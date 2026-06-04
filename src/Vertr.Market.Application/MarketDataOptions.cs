namespace Vertr.Market.Application;

public sealed class MarketDataOptions
{
    public required TimeSpan PublishingInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    public required int SnapshotCapacity { get; set; } = 64;
    public required int RingBufferSize { get; set; } = 1024;
    public required bool ResetBufferAfterPublish { get; set; }
}