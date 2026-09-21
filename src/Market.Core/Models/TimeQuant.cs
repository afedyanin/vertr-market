using MessagePack;

namespace Market.Core.Models;

[MessagePackObject]
public readonly struct TimeQuant
{
    [Key(0)]
    public readonly ushort AssetId { get; }

    [Key(1)]
    public readonly long Timestamp { get; }

    [Key(2)]
    public readonly decimal Open { get; }

    [Key(3)]
    public readonly decimal High { get; }

    [Key(4)]
    public readonly decimal Low { get; }

    [Key(5)]
    public readonly decimal Close { get; }

    [Key(6)]
    public readonly uint Volume { get; }

    public TimeQuant(
        ushort assetId,
        long timestamp,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        uint volume)
    {
        AssetId = assetId;
        Timestamp = timestamp;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }

    public static TimeQuant CreateEmpty(ushort assetId, long timestamp, decimal lastPrice)
        => new TimeQuant(assetId, timestamp, lastPrice, lastPrice, lastPrice, lastPrice, 0);
}