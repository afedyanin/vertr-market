using Vertr.Market.Tinvest;

namespace Vertr.Market.Host.BackgroundServices;

public class TinvestMarketDataConsumerService : BackgroundService
{
    private readonly MarketDataStreamClient _marketDataStreamClient;

    public TinvestMarketDataConsumerService(MarketDataStreamClient marketDataStreamClient)
    {
        _marketDataStreamClient = marketDataStreamClient;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _marketDataStreamClient.ExecuteAsync(stoppingToken);
}
