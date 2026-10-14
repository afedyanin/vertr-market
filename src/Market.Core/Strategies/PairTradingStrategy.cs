using System.Threading.Channels;
using Market.Core.Models;
using Microsoft.Extensions.Logging;

namespace Market.Core.Strategies;

public class PairTradingStrategy
{
    private readonly ushort _assetX;
    private readonly ushort _assetY;
    private readonly IPairTradingAlgo _algo;
    private readonly ChannelReader<OrderBookDepth> _reader;
    private readonly ChannelWriter<IPairTradingSignal> _writer;
    private readonly ILogger<PairTradingStrategy> _logger;

    public PairTradingStrategy(
        IPairTradingAlgo algo,
        Channel<OrderBookDepth> orderBooksChannel,
        Channel<IPairTradingSignal> signalsChannel,
        ILoggerFactory loggerFactory)
    {
        _assetX = algo.AssetX;
        _assetY = algo.AssetY;
        _algo = algo;
        _reader = orderBooksChannel.Reader;
        _writer = signalsChannel.Writer;
        _logger = loggerFactory.CreateLogger<PairTradingStrategy>();
    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"Order Book Pair Trading Strategy started. AssetX={_assetX} AssetY={_assetY}");

        try
        {
            while (await _reader.WaitToReadAsync(stoppingToken))
            {
                while (_reader.TryRead(out var orderBook))
                {
                    if (orderBook.AssetId != _assetX &&
                        orderBook.AssetId != _assetY)
                    {
                        continue;
                    }

                    var signal = _algo.ProcessBook(orderBook);

                    if (signal is null)
                    {
                        continue;
                    }

                    _logger.LogInformation("Sending signal: {Signal}.", signal);
                    _writer.TryWrite(signal);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Order Book Pair Trading Strategy cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order book processing error. Message={Mesage}", ex.Message);
        }
        finally
        {
            _logger.LogInformation("Order Book Pair Trading Strategy stopped.");
        }
    }
}
