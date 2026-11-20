using System.Threading.Channels;
using Market.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Market.BacktestEngine.BackgroundServices;

public class DumpTradingSignalsService : BackgroundService
{
    private readonly ChannelReader<PairTradingSignal> _reader;
    private readonly ILogger<DumpOrderBooksService> _logger;

    public DumpTradingSignalsService(
        Channel<PairTradingSignal> channel,
        ILoggerFactory loggerFactory)
    {
        _reader = channel.Reader;
        _logger = loggerFactory.CreateLogger<DumpOrderBooksService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting dump trading signals service...");

        try
        {
            await foreach (var signal in _reader.ReadAllAsync(stoppingToken))
            {
                _logger.LogInformation("Received trading signal: {Signal}", signal);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dump trading signals service error. Message={Message}", ex.Message);
        }

        _logger.LogInformation("Dump trading signals service stopped.");
    }
}
