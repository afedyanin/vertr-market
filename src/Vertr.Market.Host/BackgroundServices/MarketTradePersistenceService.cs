using Vertr.Common.Contracts;
using Vertr.Common.Contracts.Abstractions;
using Vertr.Market.Application.Abstractions;

namespace Vertr.Market.Host.BackgroundServices;

public class MarketTradePersistenceService : BackgroundService
{
    private readonly IMarketTradeRepository _marketTradeRepository;
    private readonly ITimeKeyedLocalStorage<MarketTrade> _marketTradeLocalStorage;
    private readonly ILogger<MarketTradePersistenceService> _logger;

    public MarketTradePersistenceService(
        IMarketTradeRepository marketTradeRepository,
        ITimeKeyedLocalStorage<MarketTrade> marketTradeLocalStorage,
        ILogger<MarketTradePersistenceService> logger)
    {
        _marketTradeRepository = marketTradeRepository;
        _marketTradeLocalStorage = marketTradeLocalStorage;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            await PersistMarketTrades();
        }
    }

    private async Task PersistMarketTrades()
    {
        var timeBefore = DateTime.UtcNow.AddMinutes(-1);
        var keys = _marketTradeLocalStorage.GetAllKeys();

        foreach (var key in keys)
        {
            var items = _marketTradeLocalStorage.RemoveBefore(key, timeBefore);
            var saved = await _marketTradeRepository.Save(timeBefore, items);

            if (!saved)
            {
                _logger.LogError("Cannot save market trades for Key={Key} TimeBefore={TimeBefore:O}", key, timeBefore);
            }
        }
    }
}
