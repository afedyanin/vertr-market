using System.Runtime.CompilerServices;

namespace Vertr.Market.Application.Models;

public struct OrderBook
{
    public int AssetId;
    public long Timestamp;
    public LevelBuffer Bids;
    public LevelBuffer Asks;
    public int BidCount;
    public int AskCount;
}

public sealed class OrderBookEvent
{
    public OrderBook OrderBook;
    public bool IsValid;
}

public readonly record struct OrderBookLevel(long Price, long Volume);

[InlineArray(10)]
public struct LevelBuffer
{
    private OrderBookLevel _element0;
}
