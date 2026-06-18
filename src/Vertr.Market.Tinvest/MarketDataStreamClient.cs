using System.Threading.Channels;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;
using Vertr.Market.Tinvest.Converters;

namespace Vertr.Market.Tinvest;

public class MarketDataStreamClient
{
    private readonly InvestApiClient _investApiClient;
    private readonly ChannelWriter<Application.Models.OrderBook> _orderBooksChannel;
    private readonly ChannelWriter<Application.Models.Trade> _tradesChannel;
    private readonly ILogger<MarketDataStreamClient> _logger;

    private readonly TimeSpan _reconnectInterval = TimeSpan.FromSeconds(5);

    private static readonly Dictionary<string, int> AssetMap = new()
    {
        { "e6123145-9665-43e0-8413-cd61b8aa9b13", 100  }, // SBER
    };

    private const bool IsEnabled = true; // TODO: Move to settings

    public MarketDataStreamClient(
        InvestApiClient investApiClient,
        ChannelWriter<Application.Models.OrderBook> orderBooksChannel,
        ChannelWriter<Application.Models.Trade> tradesChannel,
        ILogger<MarketDataStreamClient> logger)
    {
        _investApiClient = investApiClient;
        _orderBooksChannel = orderBooksChannel;
        _tradesChannel = tradesChannel;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!IsEnabled)
            {
#pragma warning disable CS0162 // Unreachable code detected
                _logger.LogWarning("{ServiceName} is disabled.", nameof(MarketDataStreamClient));
#pragma warning restore CS0162 // Unreachable code detected
                return;
            }

            await StartConsumingLoop(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, ex.Message);
        }

        _logger.LogInformation("{ServiceName} execution completed at {EndTime:O}", nameof(MarketDataStreamClient), DateTime.UtcNow);
    }

    private async Task StartConsumingLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("{ServiceName} started at {StartTime:O}", nameof(MarketDataStreamClient), DateTime.UtcNow);
                await Subscribe(deadline: null, cancellationToken);
            }
            catch (RpcException rpcEx)
            {
                if (rpcEx.StatusCode != StatusCode.DeadlineExceeded)
                {
                    _logger.LogError(rpcEx, "{ServiceName} consuming exception. Message={Message}", nameof(MarketDataStreamClient), rpcEx.Message);
                }

                await Task.Delay(_reconnectInterval, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ServiceName} consuming exception. Message={Message}", nameof(MarketDataStreamClient), ex.Message);
                await Task.Delay(_reconnectInterval, cancellationToken);
            }
        }
    }

    public async Task Subscribe(
        DateTime? deadline = null,
        CancellationToken cancellationToken = default)
    {
        var request = CreateStreamRequest();
        using var stream = _investApiClient.MarketDataStream.MarketDataServerSideStream(request, headers: null, deadline, cancellationToken);

        await foreach (var response in stream.ResponseStream.ReadAllAsync(cancellationToken))
        {
            await HandleResponse(response, cancellationToken);
        }
    }

    private MarketDataServerSideStreamRequest CreateStreamRequest()
    {
        var orderBookRequest = new SubscribeOrderBookRequest
        {
            SubscriptionAction = SubscriptionAction.Subscribe,
        };

        var tradesRequest = new SubscribeTradesRequest
        {
            SubscriptionAction = SubscriptionAction.Subscribe,
        };

        foreach (var instrumentId in AssetMap.Keys)
        {
            orderBookRequest.Instruments.Add(new OrderBookInstrument()
            {
                InstrumentId = instrumentId,
                Depth = Application.Models.Consts.OrderBookDepth,
                OrderBookType = OrderBookType.All,
            });

            tradesRequest.Instruments.Add(new TradeInstrument()
            {
                InstrumentId = instrumentId,
            });
        }

        var request = new MarketDataServerSideStreamRequest()
        {
            SubscribeOrderBookRequest = orderBookRequest,
            SubscribeTradesRequest = tradesRequest
        };

        return request;
    }

    private async Task HandleResponse(MarketDataResponse? response, CancellationToken cancellationToken)
    {
        if (response == null)
        {
            return;
        }

        if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.Orderbook)
        {
            await HandleOrderBook(response.Orderbook, cancellationToken);
            return;
        }

        if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.Candle)
        {
            await HandleCandle(response.Candle, cancellationToken);
            return;
        }

        if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.SubscribeCandlesResponse)
        {
            await HandleSubscribeCandlesResponse(response.SubscribeCandlesResponse, cancellationToken);
            return;
        }

        if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.Ping)
        {
            await HandlePing(response.Ping, cancellationToken);
            return;
        }

        if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.SubscribeOrderBookResponse)
        {
            await HandleSubscribeOrderBookResponse(response.SubscribeOrderBookResponse, cancellationToken);
            return;
        }

        if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.Trade)
        {
            await HandleTrade(response.Trade, cancellationToken);
            return;
        }

        if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.SubscribeTradesResponse)
        {
            await HandleSubscribeTradesResponse(response.SubscribeTradesResponse, cancellationToken);
            return;
        }

        if (response.PayloadCase == MarketDataResponse.PayloadOneofCase.OpenInterest)
        {
            await HandleOpenInterest(response.OpenInterest, cancellationToken);
            return;
        }
    }
    private async Task HandleOrderBook(OrderBook orderBook, CancellationToken cancellationToken)
    {
        _logger.LogDebug("OrderBook received: {OrderBook}", orderBook);

        if (!orderBook.IsConsistent)
        {
            return;
        }

        if (!AssetMap.TryGetValue(orderBook.InstrumentUid, out var assetId))
        {
            return;
        }

        await _orderBooksChannel.WriteAsync(orderBook.Convert(assetId), cancellationToken);
    }
    private async Task HandleTrade(Trade trade, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Trade received: {Trade}", trade);

        if (!AssetMap.TryGetValue(trade.InstrumentUid, out var assetId))
        {
            return;
        }

        await _tradesChannel.WriteAsync(trade.Convert(assetId), cancellationToken);
    }

    private ValueTask HandleCandle(Candle candle, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Candle received: {Candle}", candle);
        return ValueTask.CompletedTask;
    }

    private ValueTask HandleSubscribeCandlesResponse(SubscribeCandlesResponse subscribeCandlesResponse, CancellationToken cancellationToken)
    {
        _logger.LogDebug("SubscribeCandlesResponse received: {SubscribeCandlesResponse}", subscribeCandlesResponse);
        return ValueTask.CompletedTask;
    }

    private ValueTask HandlePing(Ping ping, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Ping received: {Ping}", ping);
        return ValueTask.CompletedTask;
    }

    private ValueTask HandleSubscribeOrderBookResponse(SubscribeOrderBookResponse subscribeOrderBookResponse, CancellationToken cancellationToken)
    {
        _logger.LogDebug("SubscribeOrderBookResponse received: {SubscribeOrderBookResponse}", subscribeOrderBookResponse);
        return ValueTask.CompletedTask;
    }

    private ValueTask HandleSubscribeTradesResponse(SubscribeTradesResponse subscribeTradesResponse, CancellationToken cancellationToken)
    {
        _logger.LogDebug("SubscribeTradesResponse received: {SubscribeTradesResponse}", subscribeTradesResponse);
        return ValueTask.CompletedTask;
    }

    private ValueTask HandleOpenInterest(OpenInterest openInterest, CancellationToken cancellationToken)
    {
        _logger.LogDebug("OpenInterest received: {OpenInterest}", openInterest);
        return ValueTask.CompletedTask;
    }
}
