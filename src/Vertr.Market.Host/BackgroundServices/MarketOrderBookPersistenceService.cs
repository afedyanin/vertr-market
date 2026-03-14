using Vertr.Common.Contracts;
using Vertr.Common.Contracts.Abstractions;
using Vertr.Market.Application.Abstractions;

namespace Vertr.Market.Host.BackgroundServices;

public class MarketOrderBookPersistenceService : BackgroundService
{
    private readonly IOrderBookRepository _orderBookRepository;
    private readonly ITimeKeyedLocalStorage<OrderBook> _orderBookLocalStorage;
    private readonly ILogger<MarketOrderBookPersistenceService> _logger;

    public MarketOrderBookPersistenceService(
        IOrderBookRepository orderBookRepository,
        ITimeKeyedLocalStorage<OrderBook> orderBookLocalStorage,
        ILogger<MarketOrderBookPersistenceService> logger)
    {
        _orderBookRepository = orderBookRepository;
        _orderBookLocalStorage = orderBookLocalStorage;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            await PersistOrderBooks();
        }
    }

    private async Task PersistOrderBooks()
    {
        var timeBefore = DateTime.UtcNow.AddMinutes(-1);
        var keys = _orderBookLocalStorage.GetAllKeys();

        foreach (var key in keys)
        {
            var items = _orderBookLocalStorage.RemoveBefore(key, timeBefore);
            var saved = await _orderBookRepository.Save(timeBefore, items);

            if (!saved)
            {
                _logger.LogError("Cannot save order books for Key={Key} TimeBefore={TimeBefore:O}", key, timeBefore);
            }
        }
    }
}
