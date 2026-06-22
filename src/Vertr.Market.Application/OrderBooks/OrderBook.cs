using System.Runtime.CompilerServices;

namespace Vertr.Market.Application.OrderBooks;


public readonly record struct OrderBook(
    int AssetId,
    DateTime Timestamp,
    LevelBuffer Bids,
    LevelBuffer Asks);

[InlineArray(Consts.OrderBookDepth)]
public struct LevelBuffer
{
    private OrderBookLevel _element0;
}

public readonly record struct OrderBookLevel(decimal Price, long Volume);

public enum OrderBookEventType
{
    OrderBook,
    TimerTick
}

public sealed class OrderBookEvent
{
    public OrderBookEventType Type { get; set; }
    public OrderBook OrderBook { get; set; }
    public DateTime TimerTimestamp { get; set; }
}