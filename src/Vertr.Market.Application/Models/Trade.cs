namespace Vertr.Market.Application.Models;

public readonly record struct Trade(
    int AssetId,
    decimal Price,
    decimal Volume,
    DateTime Timestamp
);

public sealed class TradeEvent
{
    public Trade Trade;
}

public struct Candle
{
    public int AssetId;
    public DateTime OpenTime;
    public decimal Open;
    public decimal High;
    public decimal Low;
    public decimal Close;
    public decimal Volume;
    public bool IsInitialized;
}

