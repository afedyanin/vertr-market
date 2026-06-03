using Vertr.Market.Application;

namespace Vertr.Market.Host.BackgroundServices;

internal sealed class SynteticDataGenerationService : BackgroundService
{
    private readonly SignalManager _signalManager;
    private readonly ILogger<SynteticDataGenerationService> _logger;

    private readonly Random _random;

    public SynteticDataGenerationService(
        SignalManager signalManager,
        ILogger<SynteticDataGenerationService> logger)
    {
        _signalManager = signalManager;
        _logger = logger;
        _random = new Random(17);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SynteticDataGenerationService starting...");

        var counter = 0L;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                FillMarketData(counter++, 25);
                await Task.Delay(_random.Next(2, 200), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MarketDataPeriodicService failed to capture snapshot");
            }
        }

        _logger.LogInformation("SynteticDataGenerationService stopped.");
    }

    private void FillMarketData(double value, int writesCount)
    {
        for (var i = 0; i <= writesCount; i++)
        {
            var index = _random.Next(0, _signalManager.Capacity);
            _signalManager.WriteSignal(index, value);
        }
    }
}
