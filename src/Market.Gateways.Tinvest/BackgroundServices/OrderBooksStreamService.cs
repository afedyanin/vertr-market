using System.Threading.Channels;
using Grpc.Core;
using Market.Core.Models;
using Microsoft.Extensions.Options;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;

namespace Market.Gateways.Tinvest.BackgroundServices;

internal sealed class OrderBooksStreamService : StreamServiceBase
{
    private readonly Channel<OrderBookDepth> _marketDepthChannel;
    protected override ILogger Logger { get; init; }
    protected override bool IsEnabled { get; init; }

    public OrderBooksStreamService(
        IServiceProvider serviceProvider,
        IOptions<TinvestSettings> tinvestOptions,
        ILoggerFactory loggerFactory) : base(serviceProvider, tinvestOptions, loggerFactory)
    {
        Logger = loggerFactory.CreateLogger<OrderBooksStreamService>();
        IsEnabled = _tinvestSettings.DataStreamEnabled;

        _marketDepthChannel = serviceProvider.GetRequiredService<Channel<OrderBookDepth>>();
    }

    protected override async Task Subscribe(Action onDataReceived, CancellationToken stoppingToken = default)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var investApiClient = scope.ServiceProvider.GetRequiredService<InvestApiClient>();
        var staticDataProvider = scope.ServiceProvider.GetRequiredService<IStaticDataProvider>();

        var orderBookRequest = new SubscribeOrderBookRequest
        {
            SubscriptionAction = SubscriptionAction.Subscribe,
        };

        foreach (var sub in _tinvestSettings.Subscriptions)
        {
            var asset = staticDataProvider.GetByTicker(sub);

            if (asset is null)
            {
                Logger.LogWarning("Annot find asset by ticker: {Ticker}", sub);
                continue;
            }

            orderBookRequest.Instruments.Add(new OrderBookInstrument()
            {
                InstrumentId = asset.ExternalId,
                Depth = _tinvestSettings.OrderBookDepth,
                OrderBookType = OrderBookType.All,
            });
        }

        var request = new MarketDataServerSideStreamRequest()
        {
            SubscribeOrderBookRequest = orderBookRequest,
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
                Logger.LogInformation("Candle subscriptions received: candle={Candle}", response.Candle);
                continue;
            }

            if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.SubscribeCandlesResponse)
            {
                var subs = response.SubscribeCandlesResponse;
                var all = subs.CandlesSubscriptions.ToArray();

                Logger.LogInformation($"Candle subscriptions received: TrackingId={subs.TrackingId} Details={string.Join(',',
                    [.. all.Select(s => $"Id={s.SubscriptionId} Status={s.SubscriptionStatus} Instrument={s.InstrumentUid} Inverval={s.Interval}")])}");
                continue;
            }

            if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.Ping)
            {
                Logger.LogDebug("Candle ping received: {Ping}", response.Ping);
                continue;
            }

            if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.Orderbook)
            {
                if (response.Orderbook != null && response.Orderbook.IsConsistent)
                {
                    var asset = staticDataProvider.GetByExternalId(response.Orderbook.InstrumentUid);

                    if (asset is null)
                    {
                        Logger.LogWarning("Skipping order book for unknown instrument: {InstrumentUid}", response.Orderbook.InstrumentUid);
                        continue;
                    }

                    var book = TinvestMapper.ToMarketDepth(response.Orderbook, asset.Id);

                    _marketDepthChannel.Writer.TryWrite(book);

                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        Logger.LogDebug("{OrderBook}", response.Orderbook);
                    }
                }

                continue;
            }

            if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.SubscribeOrderBookResponse)
            {
                var subs = response.SubscribeOrderBookResponse;
                var all = subs.OrderBookSubscriptions.ToArray();

                Logger.LogInformation($"Order book subscriptions received: TrackingId={subs.TrackingId} Details={string.Join(',',
                    [.. all.Select(s => $"Id={s.SubscriptionId} Status={s.SubscriptionStatus} Instrument={s.InstrumentUid} Depth={s.Depth}")])}");
                continue;
            }
        }
    }

    protected override ValueTask OnComplete()
    {
        _marketDepthChannel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
