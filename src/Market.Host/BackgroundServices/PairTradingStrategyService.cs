using Market.Core.Strategies;
namespace Market.Host.BackgroundServices;

public class PairTradingStrategyService : BackgroundService
{
    // TODO: Implement multiple strategies host
    private readonly PairTradingStrategy _pairTradingStrategy;

    public PairTradingStrategyService(PairTradingStrategy pairTradingStrategy)
    {
        _pairTradingStrategy = pairTradingStrategy;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _pairTradingStrategy.ExecuteAsync(stoppingToken);
    }
}
