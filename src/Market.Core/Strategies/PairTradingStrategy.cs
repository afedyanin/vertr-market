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
    private readonly ChannelWriter<PairTradingSignal> _writer;
    private readonly ILogger<PairTradingStrategy> _logger;

    public PairTradingStrategy(
        IPairTradingAlgo algo,
        ushort assetX,
        ushort assetY,
        Channel<OrderBookDepth> orderBooksChannel,
        Channel<PairTradingSignal> signalsChannel,
        ILoggerFactory loggerFactory)
    {
        _assetX = assetX;
        _assetY = assetY;
        _algo = algo;
        _reader = orderBooksChannel.Reader;
        _writer = signalsChannel.Writer;
        _logger = loggerFactory.CreateLogger<PairTradingStrategy>();
    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Order Book Pair Trading Strategy started.");

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

                    var signal = orderBook.AssetId == _assetX
                            ? _algo.ProcessTickX(orderBook)
                            : _algo.ProcessTickY(orderBook);

                    if (signal is null)
                    {
                        continue;
                    }

                    _logger.LogDebug("Sending signal: {Signal}.", signal);
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
