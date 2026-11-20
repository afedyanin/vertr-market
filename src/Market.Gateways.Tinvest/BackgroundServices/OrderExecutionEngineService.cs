using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Grpc.Core;
using Market.Core.Models;
using Market.Core.Repositories;
using Market.Infrastructure.Execution;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;

namespace Market.Gateways.Tinvest.BackgroundServices;

/// <summary>
/// Исполняет торговые сигналы: отправляет обе ноги пары, связывает сделки
/// с <c>StrategyId</c> и публикует результат в Redis.
/// </summary>
public sealed class OrderExecutionEngineService : BackgroundService
{
    private static readonly TimeSpan PendingTradesTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OrderMappingTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    private readonly ChannelReader<PairTradingSignal> _signalsReader;
    private readonly ChannelReader<OrderTrades> _orderTradesReader;
    private readonly ChannelWriter<MyTrade> _myTradesWriter;
    private readonly IStaticDataProvider _staticDataProvider;
    private readonly TinvestSettings _tinvestSettings;
    private readonly InvestApiClient _apiClient;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger _logger;

    // OrderRequestId -> StrategyId
    private readonly ConcurrentDictionary<string, StrategyMapping> _requestId2StrategyId = new(StringComparer.Ordinal);

    // OrderId -> OrderRequestId
    private readonly ConcurrentDictionary<string, OrderMapping> _orderId2RequestId = new(StringComparer.Ordinal);

    // Последняя задача обработки сигнала для каждой стратегии.
    // Гарантирует, что сигналы одной стратегии не выполняются параллельно.
    private readonly ConcurrentDictionary<Guid, Task> _strategyTails = new();

    private readonly PendingTradeBuffer _pendingTrades = new(PendingTradesTtl);

    private readonly IDatabase _redisDb;
    private readonly ConcurrentDictionary<ushort, RedisChannel> _requestsChannelCache = new();
    private readonly ConcurrentDictionary<ushort, RedisChannel> _responsesChannelCache = new();
    private readonly ConcurrentDictionary<ushort, RedisChannel> _errorsChannelCache = new();

    public OrderExecutionEngineService(
        IConnectionMultiplexer redis,
        Channel<PairTradingSignal> signalsChannel,
        Channel<OrderTrades> orderTradesChannel,
        Channel<MyTrade> tradesChannel,
        IOptions<TinvestSettings> tinvestOptions,
        InvestApiClient apiClient,
        IStaticDataProvider staticDataProvider,
        IHostApplicationLifetime lifetime,
        ILoggerFactory loggerFactory)
    {
        _signalsReader = signalsChannel.Reader;
        _orderTradesReader = orderTradesChannel.Reader;
        _myTradesWriter = tradesChannel.Writer;
        _tinvestSettings = tinvestOptions.Value;
        _apiClient = apiClient;
        _staticDataProvider = staticDataProvider;
        _lifetime = lifetime;
        _redisDb = redis.GetDatabase();
        _logger = loggerFactory.CreateLogger<OrderExecutionEngineService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var signalsTask = HandleTradingSignals(stoppingToken);
            var orderTradesTask = HandleOrderTrades(stoppingToken);
            var sweepTask = SweepExpiredState(stoppingToken);

            await Task.WhenAll(signalsTask, orderTradesTask, sweepTask);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // Молчаливая остановка движка опаснее падения процесса: сигналы
            // продолжали бы приходить, но никто бы их не исполнял.
            _logger.LogCritical(ex, "Order execution engine failed. Stopping application.");
            _lifetime.StopApplication();
        }
    }

    private async Task HandleTradingSignals(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var signal in _signalsReader.ReadAllAsync(cancellationToken))
            {
                if (!IsSignalFresh(signal))
                {
                    _logger.LogWarning(
                        "Stale trading signal skipped. StrategyId={StrategyId} BookTimestamp={BookTimestamp}",
                        signal.StrategyId,
                        signal.BookY.Timestamp);
                    continue;
                }

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Trading signal received: {Signal}", signal);
                }

                var previous = _strategyTails.GetValueOrDefault(signal.StrategyId) ?? Task.CompletedTask;
                var current = ProcessSignalAfterAsync(previous, signal, cancellationToken);
                _strategyTails[signal.StrategyId] = current;
            }
        }
        finally
        {
            // Дожидаемся in-flight сигналов при штатной остановке.
            await Task.WhenAll(_strategyTails.Values);
        }
    }

    /// <summary>
    /// Последовательно обрабатывает сигналы одной стратегии: следующий ждёт
    /// завершения предыдущего, чтобы не открыть позицию дважды.
    /// </summary>
    private async Task ProcessSignalAfterAsync(
        Task previous,
        PairTradingSignal signal,
        CancellationToken cancellationToken)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch
        {
            // Ошибка предыдущего сигнала уже залогирована внутри цепочки.
        }

        try
        {
            await PostOrders(signal, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot post orders. StrategyId={StrategyId}", signal.StrategyId);
        }
    }

    private bool IsSignalFresh(PairTradingSignal signal)
    {
        var maxAge = _tinvestSettings.SignalMaxAge;

        if (maxAge <= TimeSpan.Zero || signal.BookY.Timestamp == default)
        {
            return true;
        }

        return DateTimeOffset.UtcNow - ToUtc(signal.BookY.Timestamp) <= maxAge;
    }

    private static DateTimeOffset ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => new DateTimeOffset(value),
        DateTimeKind.Local => new DateTimeOffset(value.ToUniversalTime()),
        _ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)),
    };

    private async Task HandleOrderTrades(CancellationToken cancellationToken)
    {
        await foreach (var orderTrades in _orderTradesReader.ReadAllAsync(cancellationToken))
        {
            try
            {
                var asset = _staticDataProvider.GetByExternalId(orderTrades.InstrumentUid);

                if (asset is null)
                {
                    _logger.LogWarning(
                        "Trades with unknown asset received. InstrumentUid={InstrumentUid} OrderId={OrderId}",
                        orderTrades.InstrumentUid,
                        orderTrades.OrderId);
                    continue;
                }

                var strategyId = GetStrategyId(orderTrades.OrderId);
                var trades = TinvestMapper.ToMyTrades(orderTrades, asset.Id, strategyId).ToArray();

                if (trades.Length == 0)
                {
                    continue;
                }

                if (strategyId is { } resolved)
                {
                    PublishTrades(trades);
                    continue;
                }

                var entry = _pendingTrades.Add(orderTrades.OrderId, trades);

                // Поток исполнения мог опубликовать маппинг между первой проверкой
                // и добавлением в буфер. Сливаем и запись (если она ещё в словаре),
                // и собственный handle — так сделка не теряется при любом порядке.
                if (GetStrategyId(orderTrades.OrderId) is { } lateResolved)
                {
                    PublishTrades(_pendingTrades.TryDrain(orderTrades.OrderId, lateResolved));
                    PublishTrades(_pendingTrades.Drain(entry, lateResolved));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling order trades. OrderId={OrderId}", orderTrades.OrderId);
            }
        }
    }

    private async Task PostOrders(PairTradingSignal signal, CancellationToken cancellationToken)
    {
        if (!TryCreateLegs(signal, out var legs))
        {
            return;
        }

        var results = await Task
            .WhenAll(legs.Select(leg => PostOrderAsync(leg, signal.StrategyId, cancellationToken)))
            .ConfigureAwait(false);

        var succeeded = results.Where(r => r.Outcome == LegOutcome.Succeeded).ToArray();
        var failed = results.Where(r => r.Outcome != LegOutcome.Succeeded).ToArray();

        if (failed.Length == 0)
        {
            return;
        }

        if (succeeded.Length == 0)
        {
            _logger.LogError(
                "Both order legs failed. StrategyId={StrategyId} Reasons={Reasons}",
                signal.StrategyId,
                string.Join("; ", failed.Select(f => f.Error)));
            return;
        }

        if (failed.Any(f => f.Outcome == LegOutcome.Unknown))
        {
            // Не знаем, исполнилась ли вторая нога: слепое закрытие может
            // удвоить позицию. Требуется ручное вмешательство.
            _logger.LogCritical(
                "One leg failed while the other has unknown state; automatic hedging is unsafe. StrategyId={StrategyId}. Manual intervention required.",
                signal.StrategyId);
            _lifetime.StopApplication();
            return;
        }

        _logger.LogCritical(
            "One order leg failed; closing the executed leg. StrategyId={StrategyId} FailedAsset={FailedAsset}",
            signal.StrategyId,
            failed[0].Leg.Asset.Id);

        foreach (var result in succeeded)
        {
            await CompensateAsync(result, signal.StrategyId, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool TryCreateLegs(PairTradingSignal signal, out LegRequest[] legs)
    {
        legs = [];

        if (!PairSignalValidator.TryValidate(signal.XQtyLots, signal.YQtyLots, out var error))
        {
            _logger.LogError(
                "Invalid trading signal. StrategyId={StrategyId} Reason={Reason} XQty={XQty} YQty={YQty}",
                signal.StrategyId,
                error,
                signal.XQtyLots,
                signal.YQtyLots);
            return false;
        }

        var assetX = _staticDataProvider.GetByAssetId(signal.BookX.AssetId);
        var assetY = _staticDataProvider.GetByAssetId(signal.BookY.AssetId);

        if (assetX is null || assetY is null
            || string.IsNullOrWhiteSpace(assetX.ExternalId)
            || string.IsNullOrWhiteSpace(assetY.ExternalId))
        {
            _logger.LogError(
                "Cannot resolve both assets of the pair; signal skipped. StrategyId={StrategyId} AssetX={AssetX} AssetY={AssetY}",
                signal.StrategyId,
                signal.BookX.AssetId,
                signal.BookY.AssetId);
            return false;
        }

        var timestamp = signal.BookY.Timestamp;

        legs =
        [
            new LegRequest(
                CreateMarketRequest(
                    signal.XQtyLots,
                    assetX,
                    OrderIdFactory.Create(signal.StrategyId, timestamp, assetX.Id, signal.XQtyLots)),
                assetX,
                signal.XQtyLots),
            new LegRequest(
                CreateMarketRequest(
                    signal.YQtyLots,
                    assetY,
                    OrderIdFactory.Create(signal.StrategyId, timestamp, assetY.Id, signal.YQtyLots)),
                assetY,
                signal.YQtyLots),
        ];

        return true;
    }

    private async Task<LegResult> PostOrderAsync(
        LegRequest leg,
        Guid strategyId,
        CancellationToken cancellationToken)
    {
        var request = leg.Request;
        _requestId2StrategyId[request.OrderId] = new StrategyMapping(strategyId, DateTimeOffset.UtcNow);

        await PublishRequestAsync(leg).ConfigureAwait(false);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ApplyRequestTimeout(timeoutCts);

            var response = await _apiClient.Orders
                .PostOrderAsync(request, cancellationToken: timeoutCts.Token)
                .ConfigureAwait(false);

            await PublishResponseAsync(leg, response).ConfigureAwait(false);

            var definitelyFailed = response.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusRejected
                || (response.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusCancelled
                    && response.LotsExecuted == 0);

            if (definitelyFailed)
            {
                _requestId2StrategyId.TryRemove(request.OrderId, out _);
                _logger.LogError(
                    "Order rejected or cancelled without execution. OrderId={OrderId} Status={Status} Reason={Reason}",
                    request.OrderId,
                    response.ExecutionReportStatus,
                    response.Message);

                await PublishErrorAsync(leg, response.Message).ConfigureAwait(false);

                return new LegResult(leg, LegOutcome.DefinitelyFailed, response, response.Message);
            }

            _orderId2RequestId[response.OrderId] = new OrderMapping(response.OrderRequestId, DateTimeOffset.UtcNow);

            // Сделки могли прийти раньше ответа на PostOrder.
            PublishTrades(_pendingTrades.TryDrain(response.OrderId, strategyId));

            return new LegResult(leg, LegOutcome.Succeeded, response, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                "Order request timed out; execution state is unknown. OrderId={OrderId}",
                request.OrderId);

            await PublishErrorAsync(leg, "timeout").ConfigureAwait(false);
            return new LegResult(leg, LegOutcome.Unknown, null, "timeout");
        }
        catch (RpcException ex) when (IsDefiniteFailure(ex.StatusCode))
        {
            _requestId2StrategyId.TryRemove(request.OrderId, out _);
            _logger.LogError(
                "Order rejected by API. OrderId={OrderId} StatusCode={StatusCode} Reason={Reason}",
                request.OrderId,
                ex.StatusCode,
                ex.Status.Detail);

            await PublishErrorAsync(leg, ex.Status.Detail).ConfigureAwait(false);
            return new LegResult(leg, LegOutcome.DefinitelyFailed, null, ex.Status.Detail);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Order request failed with unknown state. OrderId={OrderId}",
                request.OrderId);

            await PublishErrorAsync(leg, ex.Message).ConfigureAwait(false);
            return new LegResult(leg, LegOutcome.Unknown, null, ex.Message);
        }
    }

    private async Task CompensateAsync(
        LegResult result,
        Guid strategyId,
        CancellationToken cancellationToken)
    {
        var response = result.Response;

        if (response is null)
        {
            return;
        }

        try
        {
            var executedLots = response.LotsExecuted;

            if (executedLots > int.MaxValue)
            {
                throw new InvalidOperationException($"Unexpected executed lots count: {executedLots}.");
            }

            if (executedLots > 0)
            {
                var compensationQty = result.Leg.Request.Direction == OrderDirection.Buy
                    ? -(int)executedLots
                    : (int)executedLots;

                var compensation = CreateMarketRequest(
                    compensationQty,
                    result.Leg.Asset,
                    OrderIdFactory.CreateCompensation(result.Leg.Request.OrderId));

                var compensationLeg = new LegRequest(compensation, result.Leg.Asset, compensationQty);
                _requestId2StrategyId[compensation.OrderId] = new StrategyMapping(strategyId, DateTimeOffset.UtcNow);

                await PublishRequestAsync(compensationLeg).ConfigureAwait(false);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                ApplyRequestTimeout(timeoutCts);

                var compensationResponse = await _apiClient.Orders
                    .PostOrderAsync(compensation, cancellationToken: timeoutCts.Token)
                    .ConfigureAwait(false);

                await PublishResponseAsync(compensationLeg, compensationResponse).ConfigureAwait(false);

                _orderId2RequestId[compensationResponse.OrderId] =
                    new OrderMapping(compensationResponse.OrderRequestId, DateTimeOffset.UtcNow);

                _logger.LogCritical(
                    "Unhedged leg compensated. StrategyId={StrategyId} OriginalOrderId={OriginalOrderId} CompensationOrderId={CompensationOrderId} Lots={Lots}",
                    strategyId,
                    result.Leg.Request.OrderId,
                    compensationResponse.OrderId,
                    executedLots);

                PublishTrades(_pendingTrades.TryDrain(compensationResponse.OrderId, strategyId));
                return;
            }

            if (response.ExecutionReportStatus is OrderExecutionReportStatus.ExecutionReportStatusNew
                or OrderExecutionReportStatus.ExecutionReportStatusPartiallyfill)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                ApplyRequestTimeout(timeoutCts);

                await _apiClient.Orders.CancelOrderAsync(
                    new CancelOrderRequest
                    {
                        AccountId = _tinvestSettings.AccountId,
                        OrderId = response.OrderId,
                    },
                    cancellationToken: timeoutCts.Token).ConfigureAwait(false);

                _logger.LogCritical(
                    "Unfilled leg cancelled. StrategyId={StrategyId} OrderId={OrderId} Status={Status}",
                    strategyId,
                    response.OrderId,
                    response.ExecutionReportStatus);
                return;
            }

            _logger.LogWarning(
                "Leg has no executed lots; nothing to compensate. OrderId={OrderId} Status={Status}",
                result.Leg.Request.OrderId,
                response.ExecutionReportStatus);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                ex,
                "Compensation failed; manual intervention required. StrategyId={StrategyId} OrderId={OrderId}",
                strategyId,
                result.Leg.Request.OrderId);

            _lifetime.StopApplication();
        }
    }

    private void ApplyRequestTimeout(CancellationTokenSource timeoutCts)
    {
        if (_tinvestSettings.OrderRequestTimeout > TimeSpan.Zero)
        {
            timeoutCts.CancelAfter(_tinvestSettings.OrderRequestTimeout);
        }
    }

    private async Task PublishRequestAsync(LegRequest leg)
    {
        try
        {
            var channel = _requestsChannelCache.GetOrAdd(
                leg.Asset.Id,
                static id => new RedisChannel($"orders:requests:{id}", RedisChannel.PatternMode.Auto));

            await _redisDb.PublishAsync(
                channel,
                JsonSerializer.Serialize(leg.Request),
                CommandFlags.FireAndForget).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Cannot publish order request to Redis. OrderId={OrderId}",
                leg.Request.OrderId);
        }
    }

    private async Task PublishResponseAsync(LegRequest leg, PostOrderResponse response)
    {
        try
        {
            var channel = _responsesChannelCache.GetOrAdd(
                leg.Asset.Id,
                static id => new RedisChannel($"orders:responses:{id}", RedisChannel.PatternMode.Auto));

            await _redisDb.PublishAsync(
                channel,
                JsonSerializer.Serialize(response),
                CommandFlags.FireAndForget).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Cannot publish order response to Redis. OrderId={OrderId}",
                leg.Request.OrderId);
        }
    }

    private async Task PublishErrorAsync(LegRequest leg, string? error)
    {
        try
        {
            var channel = _errorsChannelCache.GetOrAdd(
                leg.Asset.Id,
                static id => new RedisChannel($"orders:errors:{id}", RedisChannel.PatternMode.Auto));

            var payload = JsonSerializer.Serialize(new OrderErrorEvent(
                leg.Request.OrderId,
                leg.Request.InstrumentId,
                error,
                DateTimeOffset.UtcNow));

            await _redisDb.PublishAsync(
                channel,
                payload,
                CommandFlags.FireAndForget).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Cannot publish order error to Redis. OrderId={OrderId}",
                leg.Request.OrderId);
        }
    }

    private void PublishTrades(IReadOnlyList<MyTrade> trades)
    {
        foreach (var trade in trades)
        {
            if (!_myTradesWriter.TryWrite(trade))
            {
                _logger.LogError(
                    "Cannot publish executed trade; channel is closed. OrderId={OrderId} TradeId={TradeId} StrategyId={StrategyId}",
                    trade.OrderId,
                    trade.TradeId,
                    trade.StrategyId);
            }
        }
    }

    private Guid? GetStrategyId(string orderId)
    {
        if (!_orderId2RequestId.TryGetValue(orderId, out var mapping))
        {
            return null;
        }

        if (!_requestId2StrategyId.TryGetValue(mapping.RequestId, out var strategy))
        {
            return null;
        }

        return strategy.StrategyId;
    }

    private async Task SweepExpiredState(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            var dropped = _pendingTrades.PurgeExpired();

            if (dropped > 0)
            {
                _logger.LogWarning(
                    "Dropped {TradesCount} pending trades for orders without strategy mapping.",
                    dropped);
            }

            var now = DateTimeOffset.UtcNow;

            foreach (var pair in _orderId2RequestId.ToArray())
            {
                if (now - pair.Value.CreatedAt <= OrderMappingTtl)
                {
                    continue;
                }

                if (_orderId2RequestId.TryRemove(pair.Key, out var mapping))
                {
                    _requestId2StrategyId.TryRemove(mapping.RequestId, out _);
                }
            }

            foreach (var pair in _requestId2StrategyId.ToArray())
            {
                if (now - pair.Value.CreatedAt > OrderMappingTtl)
                {
                    _requestId2StrategyId.TryRemove(pair.Key, out _);
                }
            }
        }
    }

    private Tinkoff.InvestApi.V1.PostOrderRequest CreateMarketRequest(
        int qtyLots,
        Core.Models.Asset asset,
        string orderId)
        => new()
        {
            AccountId = _tinvestSettings.AccountId,
            OrderId = orderId,
            InstrumentId = asset.ExternalId!,
            Quantity = Math.Abs(qtyLots),
            Price = decimal.Zero, // Market order
            OrderType = OrderType.Market,
            Direction = qtyLots > 0 ? OrderDirection.Buy : OrderDirection.Sell,
            TimeInForce = TimeInForceType.TimeInForceUnspecified,
            PriceType = PriceType.Unspecified,
            ConfirmMarginTrade = false,
        };

    private static bool IsDefiniteFailure(StatusCode statusCode) => statusCode is
        StatusCode.InvalidArgument or
        StatusCode.FailedPrecondition or
        StatusCode.PermissionDenied or
        StatusCode.Unauthenticated or
        StatusCode.NotFound or
        StatusCode.ResourceExhausted or
        StatusCode.OutOfRange or
        StatusCode.Unimplemented;

    private readonly record struct StrategyMapping(Guid StrategyId, DateTimeOffset CreatedAt);

    private readonly record struct OrderMapping(string RequestId, DateTimeOffset CreatedAt);

    private sealed record LegRequest(
        Tinkoff.InvestApi.V1.PostOrderRequest Request,
        Core.Models.Asset Asset,
        int QtyLots);

    private sealed record LegResult(
        LegRequest Leg,
        LegOutcome Outcome,
        PostOrderResponse? Response,
        string? Error);

    private sealed record OrderErrorEvent(
        string OrderRequestId,
        string InstrumentId,
        string? Error,
        DateTimeOffset Timestamp);

    private enum LegOutcome
    {
        Succeeded,
        DefinitelyFailed,
        Unknown,
    }
}
