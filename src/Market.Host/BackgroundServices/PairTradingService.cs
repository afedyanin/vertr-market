using Market.Core.Strategies;
using StackExchange.Redis;

namespace Market.Host.BackgroundServices;

public class PairTradingService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PairTradingService> _logger;

    public PairTradingService(
        IConnectionMultiplexer redis,
        ILoggerFactory loggerFactory)
    {
        _redis = redis;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<PairTradingService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting SBER pair trading strategy.");

        var sberStrategy = new PairTradingStrategy(1, 5, _redis, _loggerFactory);
        await sberStrategy.ExecuteAsync(stoppingToken);

        _logger.LogInformation("SBER pair trading strategy completed.");
    }
}
