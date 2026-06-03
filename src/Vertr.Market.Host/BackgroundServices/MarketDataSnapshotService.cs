using Vertr.Market.Application;

namespace Vertr.Market.Host.BackgroundServices;

internal sealed class MarketDataSnapshotService : BackgroundService
{
    private readonly MarketDataPeriodicService _marketDataPeriodicService;

    public MarketDataSnapshotService(MarketDataPeriodicService marketDataPeriodicService)
    {
        _marketDataPeriodicService = marketDataPeriodicService;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _marketDataPeriodicService.ExecuteAsync(stoppingToken);
}
