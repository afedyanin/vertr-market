using System.Threading.Channels;
using Grpc.Core;
using Market.Core.FileStore;
using Market.Core.Models;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;

namespace Market.Gateways.Tinvest.BackgroundServices;

internal sealed class TinvestBackgroundService : BackgroundService, IAsyncDisposable
{
    private sealed class MarketDepthHolder
    {
        public MarketDepth Data;
    }

    private sealed class MarketUpdateHolder
    {
        public TradeTick Data;
    }

    private readonly ILogger<TinvestBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TinvestSettings _tinvestSettings;
    private readonly string _serviceName;

    private readonly Dictionary<string, ushort> _instruments = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);

    private readonly TimeSpan _flushInterval = TimeSpan.FromMilliseconds(1500);

    private readonly MarketDepthFileWriter _marketDepthFileWriter;
    private readonly TradeTickFileWriter _marketUpdateFileWriter;

    private readonly Channel<MarketDepthHolder> _marketDepthChannel;
    private readonly Channel<MarketUpdateHolder> _marketUpdateChannel;

    private readonly ObjectPool<MarketDepthHolder> _marketDepthPool =
        new DefaultObjectPoolProvider().Create(new DefaultPooledObjectPolicy<MarketDepthHolder>());

    private readonly ObjectPool<MarketUpdateHolder> _marketUpdatePool =
        new DefaultObjectPoolProvider().Create(new DefaultPooledObjectPolicy<MarketUpdateHolder>());

    public TinvestBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<TinvestSettings> tinvestOptions,
        ILogger<TinvestBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _tinvestSettings = tinvestOptions.Value;
        _logger = logger;
        _serviceName = GetType().Name;

        _marketDepthFileWriter = new MarketDepthFileWriter(_tinvestSettings.OutputDirectory, _flushInterval);
        _marketUpdateFileWriter = new TradeTickFileWriter(_tinvestSettings.OutputDirectory, _flushInterval);

        _marketDepthChannel = Channel.CreateBounded<MarketDepthHolder>(new BoundedChannelOptions(10000)
        {
            SingleWriter = true, // Писать в канал будет только gRPC поток
            SingleReader = true, // Читать будет один фоновый воркер записи
            FullMode = BoundedChannelFullMode.DropOldest // Если очередь полная, выкидываем старый стакан
        });

        _marketUpdateChannel = Channel.CreateBounded<MarketUpdateHolder>(new BoundedChannelOptions(10000)
        {
            SingleWriter = true, // Писать в канал будет только gRPC поток
            SingleReader = true, // Читать будет один фоновый воркер записи
            FullMode = BoundedChannelFullMode.DropOldest // Если очередь полная, выкидываем старый трейд
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_tinvestSettings.DataStreamEnabled)
        {
            _logger.LogWarning($"{_serviceName} is disabled.");
            return;
        }

        var orderBooksWritingTask = StartMarketDepthWritingLoopAsync();
        var tradesWritingTask = StartMarketUpdatesWritingLoopAsync();

        try
        {
            await StartConsumingLoop(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, $"Unhandled exception in {_serviceName} master loop: {ex.Message}");
        }
        finally
        {
            _logger.LogInformation($"Stopping token signaled. Initiating graceful shutdown for channels...");

            _marketDepthChannel.Writer.TryComplete();
            _marketUpdateChannel.Writer.TryComplete();

            await Task.WhenAll(orderBooksWritingTask, tradesWritingTask);

            _logger.LogInformation($"{_serviceName} execution completed at {DateTime.UtcNow:O}");
        }
    }

    private async Task StartConsumingLoop(CancellationToken stoppingToken)
    {
        var baseDelay = TimeSpan.FromSeconds(2);
        var maxDelay = TimeSpan.FromMinutes(1); // Защита: не ждем дольше одной минуты
        var currentDelay = baseDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation($"{_serviceName} consuming started at {DateTime.UtcNow:O}");

            try
            {
                await Subscribe(onDataReceived: () =>
                {
                    if (currentDelay != baseDelay)
                    {
                        _logger.LogInformation($"{_serviceName} connection stable. Resetting retry delay to {baseDelay.TotalSeconds}s.");
                        currentDelay = baseDelay; // Сбрасываем задержку на исходную
                    }
                }, stoppingToken);
            }
            catch (RpcException rpcEx)
            {
                if (rpcEx.StatusCode != StatusCode.DeadlineExceeded)
                {
                    _logger.LogError(rpcEx, $"{_serviceName} gRPC consuming exception. StatusCode={rpcEx.StatusCode}, Message={rpcEx.Message}");
                }

                await SafeDelay(currentDelay, stoppingToken);
                currentDelay = MultiplyDelay(currentDelay, baseDelay, maxDelay);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_serviceName} network stream exception. Message={ex.Message}");

                await SafeDelay(currentDelay, stoppingToken);
                currentDelay = MultiplyDelay(currentDelay, baseDelay, maxDelay);
            }
        }
    }

    private async Task StartMarketDepthWritingLoopAsync()
    {
        _logger.LogInformation("Market depth writing task started...");

        try
        {
            await foreach (var holder in _marketDepthChannel.Reader.ReadAllAsync())
            {
                try
                {
                    _marketDepthFileWriter.Write(in holder.Data);
                }
                finally
                {
                    _marketDepthPool.Return(holder);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "FATAL: Market depth writing loop crashed.");
        }
    }

    private async Task StartMarketUpdatesWritingLoopAsync()
    {
        _logger.LogInformation("Market updates writing task started...");

        try
        {
            await foreach (var holder in _marketUpdateChannel.Reader.ReadAllAsync())
            {
                try
                {
                    _marketUpdateFileWriter.Write(in holder.Data);
                }
                finally
                {
                    _marketUpdatePool.Return(holder);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "FATAL: Market updates writing loop crashed.");
        }
    }

    private TimeSpan MultiplyDelay(TimeSpan current, TimeSpan baseValue, TimeSpan max)
    {
        // Экспоненциальное увеличение: 2с -> 4с -> 8с -> 16с -> 32с -> 60с
        var nextDelay = TimeSpan.FromSeconds(current.TotalSeconds * 2);
        return nextDelay > max ? max : nextDelay;
    }

    private async Task SafeDelay(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogWarning($"Waiting {delay.TotalSeconds} seconds before reconnecting...");
            await Task.Delay(delay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Игнорируем, приложение завершает работу
        }
    }

    private async Task Subscribe(Action onDataReceived, CancellationToken stoppingToken = default)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var investApiClient = scope.ServiceProvider.GetRequiredService<InvestApiClient>();

        var orderBookRequest = new SubscribeOrderBookRequest
        {
            SubscriptionAction = SubscriptionAction.Subscribe,
        };

        var tradesRequest = new SubscribeTradesRequest
        {
            SubscriptionAction = SubscriptionAction.Subscribe,
            WithOpenInterest = true,
        };

        foreach (var sub in _tinvestSettings.Subscriptions)
        {
            if (sub.Disabled)
            {
                _logger.LogInformation($"Skipping subscription: InstrumentId={sub.InstrumentId}");
                continue;
            }

            orderBookRequest.Instruments.Add(new OrderBookInstrument()
            {
                InstrumentId = sub.InstrumentId.ToString(),
                Depth = _tinvestSettings.OrderBookDepth,
                OrderBookType = OrderBookType.All,
            });

            tradesRequest.Instruments.Add(new TradeInstrument()
            {
                InstrumentId = sub.InstrumentId.ToString(),
            });

            _instruments[sub.InstrumentId.ToString()] = sub.AssetId;
        }

        var request = new MarketDataServerSideStreamRequest()
        {
            SubscribeOrderBookRequest = orderBookRequest,
            SubscribeTradesRequest = tradesRequest
        };

        using var stream = investApiClient.MarketDataStream.MarketDataServerSideStream(request, headers: null, deadline: null, stoppingToken);

        var isFirstMessage = true;

        await foreach (var response in stream.ResponseStream.ReadAllAsync(stoppingToken))
        {
            // Если пришло любое сообщение из сети, сбрасываем экспоненциальный таймер в основном цикле
            if (isFirstMessage)
            {
                onDataReceived();
                isFirstMessage = false;
            }

            if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.Candle)
            {
                _logger.LogInformation("Candle subscriptions received: candle={Candle}", response.Candle);
                continue;
            }

            if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.SubscribeCandlesResponse)
            {
                var subs = response.SubscribeCandlesResponse;
                var all = subs.CandlesSubscriptions.ToArray();

                _logger.LogInformation($"Candle subscriptions received: TrackingId={subs.TrackingId} Details={string.Join(',',
                    [.. all.Select(s => $"Id={s.SubscriptionId} Status={s.SubscriptionStatus} Instrument={s.InstrumentUid} Inverval={s.Interval}")])}");
                continue;
            }

            if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.Ping)
            {
                _logger.LogDebug("Candle ping received: {Ping}", response.Ping);
                continue;
            }

            if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.Orderbook)
            {
                if (response.Orderbook != null)
                {
                    var assetId = GetAssetId(response.Orderbook.InstrumentUid);
                    var holder = _marketDepthPool.Get();
                    holder.Data = TinvestMapper.ToMarketDepth(response.Orderbook, assetId);

                    _marketDepthChannel.Writer.TryWrite(holder);

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("{OrderBook}", response.Orderbook);
                    }
                }

                continue;
            }

            if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.SubscribeOrderBookResponse)
            {
                var subs = response.SubscribeOrderBookResponse;
                var all = subs.OrderBookSubscriptions.ToArray();

                _logger.LogInformation($"Order book subscriptions received: TrackingId={subs.TrackingId} Details={string.Join(',',
                    [.. all.Select(s => $"Id={s.SubscriptionId} Status={s.SubscriptionStatus} Instrument={s.InstrumentUid} Depth={s.Depth}")])}");
                continue;
            }

            if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.Trade)
            {
                if (response.Trade != null)
                {
                    var assetId = GetAssetId(response.Trade.InstrumentUid);
                    var holder = _marketUpdatePool.Get();
                    holder.Data = TinvestMapper.ToMarketUpdate(response.Trade, assetId);

                    _marketUpdateChannel.Writer.TryWrite(holder);

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Trade received: {Trade}", response.Trade);
                    }
                }

                continue;
            }

            if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.OpenInterest)
            {
                _logger.LogDebug("Open interest received: {OpenInterest}", response.OpenInterest);
                continue;
            }

            if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.SubscribeTradesResponse)
            {
                var subs = response.SubscribeTradesResponse;
                var all = subs.TradeSubscriptions.ToArray();

                _logger.LogInformation($"Trade subscriptions received: TrackingId={subs.TrackingId} Details={string.Join(',',
                    [.. all.Select(s => $"Id={s.SubscriptionId} Status={s.SubscriptionStatus} Instrument={s.InstrumentUid} WithOpenInterest={s.WithOpenInterest}")])}");
                continue;
            }
        }
    }

    private ushort GetAssetId(string instrumentId)
    {
        _instruments.TryGetValue(instrumentId, out var assetId);
        return assetId;
    }

    public async ValueTask DisposeAsync()
    {
        if (_marketDepthFileWriter != null)
        {
            await _marketDepthFileWriter.DisposeAsync();
        }

        if (_marketUpdateFileWriter != null)
        {
            await _marketUpdateFileWriter.DisposeAsync();
        }

        Dispose();
        GC.SuppressFinalize(this);
    }
}
