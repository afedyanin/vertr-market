using MessagePack;

namespace Market.ApiClient.Models;

[MessagePackObject]
public readonly struct Candle
{
    [Key(0)]
    public readonly ushort AssetId { get; }

    [Key(1)]
    public readonly long Timestamp { get; }

    [Key(2)]
    public readonly double Open { get; }

    [Key(3)]
    public readonly double High { get; }

    [Key(4)]
    public readonly double Low { get; }

    [Key(5)]
    public readonly double Close { get; }

    [Key(6)]
    public readonly uint Volume { get; }

    public Candle(
        ushort assetId,
        long timestamp,
        double open,
        double high,
        double low,
        double close,
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

    public static Candle CreateEmpty(ushort assetId, long timestamp, double lastPrice)
        => new Candle(assetId, timestamp, lastPrice, lastPrice, lastPrice, lastPrice, 0);
}