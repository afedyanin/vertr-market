using Disruptor;
using Microsoft.Extensions.Logging;

namespace Vertr.Market.Application.EventHandlers;

internal sealed class MarketDataSnapshotLogger : IEventHandler<MarketDataSnapshot>
{
    private readonly ILogger<MarketDataSnapshotLogger> _logger;

    public MarketDataSnapshotLogger(ILogger<MarketDataSnapshotLogger> logger)
    {
        _logger = logger;
    }

    public void OnEvent(MarketDataSnapshot data, long sequence, bool endOfBatch)
    {
        _logger.LogInformation("#{Sequence} {Snapshot}", sequence, data);
    }
}
