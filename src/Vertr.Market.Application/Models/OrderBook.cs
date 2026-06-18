using System.Runtime.CompilerServices;

namespace Vertr.Market.Application.Models;

public readonly record struct OrderBook(
    int AssetId,
    DateTime Timestamp,
    LevelBuffer Bids,
    LevelBuffer Asks,
    int BidCount,
    int AskCount);

public sealed class OrderBookEvent
{
    public OrderBook OrderBook;
}

[InlineArray(10)]
public struct LevelBuffer
{
    private OrderBookLevel _element0;
}

public readonly record struct OrderBookLevel(decimal Price, long Volume);
