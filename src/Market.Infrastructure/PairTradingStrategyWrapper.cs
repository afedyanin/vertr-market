using System.Threading.Channels;
using Market.Core.Models;
using Microsoft.Extensions.Logging;

namespace Market.Infrastructure;

public class PairTradingStrategyWrapper
{
    private readonly ushort _assetX;
    private readonly ushort _assetY;
    private readonly IPairTradingAlgo _algo;
    private readonly ChannelReader<OrderBookDepth> _reader;
    private readonly ChannelWriter<IPairTradingSignal> _writer;
    private readonly ILogger<PairTradingStrategyWrapper> _logger;

    public PairTradingStrategyWrapper(
        IPairTradingAlgo algo,
        ChannelReader<OrderBookDepth> orderBooksChannelReader,
        ChannelWriter<IPairTradingSignal> signalsChannelWriter,
        ILoggerFactory loggerFactory)
    {
        _assetX = algo.AssetX;
        _assetY = algo.AssetY;
        _algo = algo;
        _reader = orderBooksChannelReader;
        _writer = signalsChannelWriter;
        _logger = loggerFactory.CreateLogger<PairTradingStrategyWrapper>();
    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"Order Book Pair Trading Strategy wrapper started. AssetX={_assetX} AssetY={_assetY}");

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

                    // Обрабатываем каждый стакан изолированно: один сбойный тик
                    // не должен останавливать стратегию целиком.
                    try
                    {
                        var signal = _algo.ProcessBook(orderBook);

                        if (signal is null)
                        {
                            continue;
                        }

                        _logger.LogInformation("Sending signal: {Signal}.", signal.ToString());
                        _writer.TryWrite(signal);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Order book processing failed for AssetId={AssetId}. Skipping book.", orderBook.AssetId);
                    }
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
