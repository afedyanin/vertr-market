using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Vertr.Market.Application.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
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

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct OrderBookLevel(long Price, long Volume);

[InlineArray(10)]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LevelBuffer
{
    private OrderBookLevel _element0;
}
