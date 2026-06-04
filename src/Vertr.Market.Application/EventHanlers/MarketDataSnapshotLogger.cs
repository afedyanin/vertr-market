using Disruptor;
using Microsoft.Extensions.Logging;

namespace Vertr.Market.Application.EventHanlers;

internal class MarketDataSnapshotLogger : IEventHandler<MarketDataSnapshot>
{
    private readonly ILogger<MarketDataSnapshotLogger> _logger;

    public MarketDataSnapshotLogger(ILogger<MarketDataSnapshotLogger> logger)
    {
        _logger = logger;
    }

    public void OnEvent(MarketDataSnapshot data, long sequence, bool endOfBatch)
    {
        _logger.LogInformation("#{Sequence} MarketDataSnapshot received.", sequence);
    }
}
