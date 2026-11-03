using Market.Infrastructure;
namespace Market.Host.BackgroundServices;

public class PairTradingStrategyService : BackgroundService
{
    private readonly OrderBooksTopic _orderBooksTopic;
    private readonly ILogger<PairTradingStrategyService> _loggerFactory;


    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
    }
}
