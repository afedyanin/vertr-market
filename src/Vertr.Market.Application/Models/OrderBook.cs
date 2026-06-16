using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Vertr.Market.Application.Models;


[StructLayout(LayoutKind.Sequential)]
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
}

public readonly record struct OrderBookLevel(decimal Price, decimal Volume);

[InlineArray(10)]
public struct LevelBuffer
{
    private OrderBookLevel _element0;
}
