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
