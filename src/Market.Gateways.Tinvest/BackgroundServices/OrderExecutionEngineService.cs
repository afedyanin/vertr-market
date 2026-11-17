using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Market.Core.Models;
using Market.Core.Repositories;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;

namespace Market.Gateways.Tinvest.BackgroundServices;

public class OrderExecutionEngineService : BackgroundService
{
    private readonly ChannelReader<PairTradingSignal> _signalsReader;
    private readonly ChannelReader<OrderTrades> _orderTradesReader;
    private readonly ChannelWriter<MyTrade> _myTradesWriter;
    private readonly IStaticDataProvider _staticDataProvider;
    private readonly TinvestSettings _tinvestSettings;
    private readonly InvestApiClient _apiClient;
    private readonly ILogger _logger;

    // OrderRequestId to StrategyId mapper
    private readonly ConcurrentDictionary<string, Guid> _requestId2StrategyId = new();

    // OrderId to OrderRequestId mapper
    private readonly ConcurrentDictionary<string, string> _orderId2requestId = new();

    // Trades by OrderId without strategyId
    private readonly ConcurrentDictionary<string, List<MyTrade>> _pendingTrades = new();

    private readonly IDatabase _redisDb;
    private readonly ConcurrentDictionary<ushort, RedisChannel> _requestsChannelCache = new();
    private readonly ConcurrentDictionary<ushort, RedisChannel> _responsesChannelCache = new();

    public OrderExecutionEngineService(
        IConnectionMultiplexer redis,
        Channel<PairTradingSignal> signalsChannel,
        Channel<OrderTrades> orderTradesChannel,
        Channel<MyTrade> tradesChannel,
        IOptions<TinvestSettings> tinvestOptions,
        InvestApiClient apiClient,
        IStaticDataProvider staticDataProvider,
        ILoggerFactory loggerFactory)
    {
        _signalsReader = signalsChannel.Reader;
        _orderTradesReader = orderTradesChannel.Reader;
        _myTradesWriter = tradesChannel.Writer;
        _tinvestSettings = tinvestOptions.Value;
        _apiClient = apiClient;
        _staticDataProvider = staticDataProvider;
        _redisDb = redis.GetDatabase();
        _logger = loggerFactory.CreateLogger<OrderExecutionEngineService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var signalsTask = HandleTradingSignals(stoppingToken);
            var orderTradesTask = HandleOrderTrades(stoppingToken);

            await Task.WhenAll(signalsTask, orderTradesTask);
        }
        catch (OperationCanceledException)
        { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing orders. Message={Message}", ex.Message);
        }
    }

    private async Task HandleTradingSignals(CancellationToken cancellationToken)
    {
        await foreach (var signal in _signalsReader.ReadAllAsync(cancellationToken))
        {
            try
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Trading signal received: {Signal}", signal);
                }

                await PostOrders(signal, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot post order. Message={Message}", ex.Message);
            }
        }
    }

    private async Task HandleOrderTrades(CancellationToken cancellationToken)
    {
        await foreach (var orderTrades in _orderTradesReader.ReadAllAsync(cancellationToken))
        {
            try
            {
                var asset = _staticDataProvider.GetByExternalId(orderTrades.InstrumentUid);

                if (asset is null)
                {
                    _logger.LogWarning("Trades with unknown asset received. InstrumentUid={InstrumentUid} OrderId={OrderId}",
                        orderTrades.InstrumentUid,
                        orderTrades.OrderId);

                    continue;
                }

                var strategyId = GetStrategyId(orderTrades.OrderId);
                var trades = TinvestMapper.ToMyTrades(orderTrades, asset.Id, strategyId);

                if (strategyId is not null)
                {
                    foreach (var trade in trades)
                    {
                        _myTradesWriter.TryWrite(trade);
                    }

                    continue;
                }

                _pendingTrades.AddOrUpdate(orderTrades.OrderId, key => [.. trades],
                    (key, old) =>
                    {
                        // НЕ потокобезопасно. Fix it
                        old.AddRange(trades);
                        return old;
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling order trades. Message={Message}", ex.Message);
            }
        }
    }

    private async Task PostOrders(PairTradingSignal signal, CancellationToken cancellationToken)
    {
        var requests = CreateRequests(signal);
        var tasks = new List<Task>(requests.Count());

        foreach (var request in requests)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Posting order request: {Request}", request);
            }

            _requestId2StrategyId.TryAdd(request.OrderId, signal.StrategyId);
            var task = PostOrderInternal(request, cancellationToken: cancellationToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
    }

    private async Task PostOrderInternal(Tinkoff.InvestApi.V1.PostOrderRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var asset = _staticDataProvider.GetByExternalId(request.InstrumentId);

            if (asset is not null)
            {
                var redisChannel = _requestsChannelCache.GetOrAdd(
                    asset.Id,
                    symbol => new RedisChannel($"orders:requests:{symbol}", RedisChannel.PatternMode.Auto));

                await _redisDb.PublishAsync(
                    redisChannel,
                    JsonSerializer.Serialize(request, request.GetType()),
                    CommandFlags.FireAndForget);
            }

            var response = await _apiClient.Orders.PostOrderAsync(request, cancellationToken: cancellationToken);

            if (asset is not null)
            {
                var redisChannel = _responsesChannelCache.GetOrAdd(
                    asset.Id,
                    symbol => new RedisChannel($"orders:responses:{symbol}", RedisChannel.PatternMode.Auto));

                await _redisDb.PublishAsync(
                    redisChannel,
                    JsonSerializer.Serialize(response, response.GetType()),
                    CommandFlags.FireAndForget);
            }

            _orderId2requestId.TryAdd(response.OrderId, response.OrderRequestId);

            if (!_requestId2StrategyId.TryGetValue(response.OrderRequestId, out var strategyId))
            {
                _logger.LogWarning("Cannot find strategyId by requestId. RequestId={RequestId}", response.OrderRequestId);
                return;
            }

            if (!_pendingTrades.TryRemove(response.OrderId, out var trades))
            {
                return;
            }

            foreach (var trade in trades)
            {
                trade.StrategyId = strategyId;
                _myTradesWriter.TryWrite(trade);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Post order error. RequestId={RequestId} Message={Message}", request.OrderId, ex.Message);
            return;
        }
    }

    private IEnumerable<Tinkoff.InvestApi.V1.PostOrderRequest> CreateRequests(PairTradingSignal signal)
    {
        var requests = new List<Tinkoff.InvestApi.V1.PostOrderRequest>();

        var assetX = _staticDataProvider.GetByAssetId(signal.BookX.AssetId);

        if (assetX is not null)
        {
            requests.Add(CreateMarketRequest(signal.XQtyLots, assetX));
        }

        var assetY = _staticDataProvider.GetByAssetId(signal.BookY.AssetId);

        if (assetY is not null)
        {
            requests.Add(CreateMarketRequest(signal.YQtyLots, assetY));
        }

        return requests;
    }

    private Guid? GetStrategyId(string orderId)
    {
        if (!_orderId2requestId.TryGetValue(orderId, out var requestId))
        {
            return null;
        }

        if (!_requestId2StrategyId.TryGetValue(requestId, out var strategyId))
        {
            return null;
        }

        return strategyId;
    }

    private Tinkoff.InvestApi.V1.PostOrderRequest CreateMarketRequest(decimal qty, Core.Models.Asset asset)
        => new Tinkoff.InvestApi.V1.PostOrderRequest
        {
            AccountId = _tinvestSettings.AccountId,
            OrderId = Guid.NewGuid().ToString(),
            InstrumentId = asset.ExternalId!,
            Quantity = (long)Math.Abs(qty), // TODO: Convert to lots
            Price = decimal.Zero, // Market order
            OrderType = OrderType.Market,
            Direction = qty > 0 ? OrderDirection.Buy : OrderDirection.Sell,
            TimeInForce = TimeInForceType.TimeInForceUnspecified,
            PriceType = PriceType.Unspecified,
            ConfirmMarginTrade = false,
        };
}
