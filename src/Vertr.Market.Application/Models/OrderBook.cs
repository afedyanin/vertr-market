using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Vertr.Market.Application.Models;


public sealed class OrderBook
{
    public int AssetId;
    public DateTime Timestamp;
    public LevelBuffer Bids;
    public LevelBuffer Asks;
    public int BidCount;
    public int AskCount;

    // Метод для очистки внутренних данных перед повторным использованием
    public void Reset()
    {
        AssetId = 0;
        Timestamp = default;
        BidCount = 0;
        AskCount = 0;
        // Очистка InlineArray (зануление памяти)
        Bids = default;
        Asks = default;
    }
}

public static class OrderBookPool
{
    private static readonly ConcurrentBag<OrderBook> Pool = new();

    public static OrderBook Rent()
    {
        return Pool.TryTake(out var book) ? book : new OrderBook();
    }

    public static void Return(OrderBook book)
    {
        book.Reset();
        Pool.Add(book);
    }
}

public sealed class OrderBookEvent
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public OrderBook OrderBook;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}

public readonly record struct OrderBookLevel(decimal Price, long Volume);

[InlineArray(10)]
public struct LevelBuffer
{
    private OrderBookLevel _element0;
}