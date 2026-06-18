namespace Vertr.Market.Application.Models;

public readonly record struct Trade(
    int AssetId,
    decimal Price,
    long Volume,
    DateTime Timestamp
);

public enum MarketTradeEventType
{
    Trade,
    TimerTick
}

public sealed class MarketTradeEvent
{
    public MarketTradeEventType Type { get; set; }
    public Trade Trade { get; set; }
    public DateTime TimerTimestamp { get; set; } // Время тика таймера
}

public struct Candle
{
    public int AssetId;
    public DateTime OpenTime;
    public decimal Open;
    public decimal High;
    public decimal Low;
    public decimal Close;
    public long Volume;
    public decimal Value;
    public bool IsInitialized;
}

