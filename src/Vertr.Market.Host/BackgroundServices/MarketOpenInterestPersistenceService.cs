using Vertr.Common.Contracts;
using Vertr.Common.Contracts.Abstractions;
using Vertr.Market.Application.Abstractions;

namespace Vertr.Market.Host.BackgroundServices;

public class MarketOpenInterestPersistenceService : BackgroundService
{
    private readonly IOpenInterestRepository _openInterestRepository;
    private readonly ITimeKeyedLocalStorage<OpenInterest> _openInterestLocalStorage;
    private readonly ILogger<MarketOpenInterestPersistenceService> _logger;

    public MarketOpenInterestPersistenceService(
        IOpenInterestRepository openInterestRepository,
        ITimeKeyedLocalStorage<OpenInterest> openInterestLocalStorage,
        ILogger<MarketOpenInterestPersistenceService> logger)
    {
        _openInterestRepository = openInterestRepository;
        _openInterestLocalStorage = openInterestLocalStorage;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            await PersistOpenInterests();
        }
    }

    private async Task PersistOpenInterests()
    {
        var timeBefore = DateTime.UtcNow.AddMinutes(-1);
        var keys = _openInterestLocalStorage.GetAllKeys();

        foreach (var key in keys)
        {
            var items = _openInterestLocalStorage.RemoveBefore(key, timeBefore).ToArray();
            var saved = await _openInterestRepository.Save(timeBefore, items);

            if (saved == 0)
            {
                _logger.LogError("Cannot save open interests for Key={Key} TimeStamp={TimeBefore:O} ItemsCount={Count}",
                    key,
                    timeBefore,
                    items.Length);
            }
        }
    }
}
