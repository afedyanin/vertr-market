namespace Vertr.Market.Application;

public sealed class MarketDataOptions
{
    public TimeSpan PublishingInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    public int SnapshotCapacity { get; set; } = 64;
    public int RingBufferSize { get; set; } = 1024;
}
