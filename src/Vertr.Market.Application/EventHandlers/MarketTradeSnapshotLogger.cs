using Disruptor;
using Microsoft.Extensions.Logging;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.EventHandlers;

internal sealed class MarketTradeSnapshotLogger : IEventHandler<MarketDataSnapshot<MarketTrade>>
{
    private readonly ILogger<MarketTradeSnapshotLogger> _logger;

    public MarketTradeSnapshotLogger(ILogger<MarketTradeSnapshotLogger> logger)
    {
        _logger = logger;
    }

    public void OnEvent(MarketDataSnapshot<MarketTrade> data, long sequence, bool endOfBatch)
    {
        _logger.LogInformation("#{Sequence} {@Snapshot}", sequence, data);
    }
}
