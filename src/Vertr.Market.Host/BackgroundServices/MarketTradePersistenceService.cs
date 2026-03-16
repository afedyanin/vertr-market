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
            var items = _marketTradeLocalStorage.RemoveBefore(key, timeBefore).ToArray();
            var saved = await _marketTradeRepository.Save(timeBefore, items);
            var itemsCount = items.Length;

            if (saved == null || saved.Value == itemsCount)
            {
                continue;
            }

            _logger.LogError("Cannot save market trades for Key={Key} TimeStamp={TimeBefore:O} ItemsCount={Count} SavedCount={Saved}",
                key,
                timeBefore,
                itemsCount,
                saved.Value);
        }
    }
}
