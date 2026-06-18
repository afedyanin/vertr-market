using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly Dictionary<string, int> _assetMap;
    private readonly bool _isEnabled;

    public MarketDataStreamClient(
        InvestApiClient investApiClient,
        Channel<Application.Models.OrderBook> orderBooksChannel,
        Channel<Application.Models.Trade> tradesChannel,
        IOptions<TinvestMarketDataSettings> options,
        ILogger<MarketDataStreamClient> logger)
    {
        _investApiClient = investApiClient;
        _orderBooksChannel = orderBooksChannel;
        _tradesChannel = tradesChannel;
        _logger = logger;
        _assetMap = options.Value.Assets;
        _isEnabled = options.Value.IsEnabled;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!_isEnabled)
            {
                _logger.LogWarning("{ServiceName} is disabled.", nameof(MarketDataStreamClient));
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

    private async Task Subscribe(
        DateTime? deadline = null,
        CancellationToken cancellationToken = default)
    {
        var request = CreateStreamRequest();

        using (var stream = _investApiClient.MarketDataStream.MarketDataServerSideStream(request, headers: null, deadline, cancellationToken))
        {
            await foreach (var response in stream.ResponseStream.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await HandleResponseAsync(response, cancellationToken).ConfigureAwait(false);
            }
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

        foreach (var instrumentId in _assetMap.Keys)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask HandleResponseAsync(MarketDataResponse? response, CancellationToken cancellationToken)
    {
        if (response == null)
        {
            return ValueTask.CompletedTask;
        }

        return response.PayloadCase switch
        {
            MarketDataResponse.PayloadOneofCase.Orderbook => HandleOrderBookAsync(response.Orderbook, cancellationToken),
            MarketDataResponse.PayloadOneofCase.Trade => HandleTradeAsync(response.Trade, cancellationToken),
            MarketDataResponse.PayloadOneofCase.Ping => HandlePing(response.Ping),
            _ => ValueTask.CompletedTask
        };
    }

    private ValueTask HandlePing(Ping ping)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Ping received: {Ping}", ping);
        }

        return ValueTask.CompletedTask;
    }

    private async ValueTask HandleOrderBookAsync(OrderBook orderBook, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("OrderBook received: {OrderBook}", orderBook);
        }

        if (!orderBook.IsConsistent || !_assetMap.TryGetValue(orderBook.InstrumentUid, out var assetId))
        {
            return;
        }

        var convertedBook = orderBook.Convert(assetId);

        // Попытка быстрой неблокирующей записи
        if (!_orderBooksChannel.TryWrite(convertedBook))
        {
            // Честное асинхронное ожидание освобождения места в буфере без дедлока потока
            await _orderBooksChannel.WriteAsync(convertedBook, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask HandleTradeAsync(Trade trade, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Trade received: {Trade}", trade);
        }

        if (!_assetMap.TryGetValue(trade.InstrumentUid, out var assetId))
        {
            return;
        }

        var convertedTrade = trade.Convert(assetId);

        if (!_tradesChannel.TryWrite(convertedTrade))
        {
            await _tradesChannel.WriteAsync(convertedTrade, cancellationToken).ConfigureAwait(false);
        }
    }
}
