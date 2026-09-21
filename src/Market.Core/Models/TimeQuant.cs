namespace Market.Core.Models;

public readonly struct TimeQuant
{
    public readonly ushort AssetId;
    public readonly DateTime Timestamp;
    public readonly decimal Open;
    public readonly decimal High;
    public readonly decimal Low;
    public readonly decimal Close;
    public readonly uint Volume;

    public TimeQuant(
        ushort assetId,
        DateTime timestamp,
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

    public static TimeQuant CreateEmpty(ushort assetId, DateTime timestamp, decimal lastPrice)
    {
        return new TimeQuant(assetId, timestamp, lastPrice, lastPrice, lastPrice, lastPrice, 0);
    }
}