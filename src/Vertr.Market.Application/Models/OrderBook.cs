using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Vertr.Market.Application.Models;


// Sequential + Pack = 1 гарантирует, что layout на диске/в сети совпадает с layout в памяти.
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct OrderBook
{
    public int AssetId;
    public long Timestamp; // Unix Timestamp

    public LevelBuffer Bids;
    public LevelBuffer Asks;

    public int BidCount;
    public int AskCount;
}

public sealed class OrderBookEvent
{
    private readonly OrderBook[] _books;

    public const int Capacity = 1024;

    public OrderBookEvent()
    {
        _books = new OrderBook[Capacity];
    }

    public void Clear()
    {
        Array.Clear(_books);
    }

    public ref readonly OrderBook this[int index] => ref _books[index];

    public void CopyTo(OrderBookEvent destination)
    {
        Array.Copy(_books, destination._books, Capacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index, in OrderBook book)
    {
        _books[index] = book;
    }
}

public readonly record struct OrderBookLevel(decimal Price, decimal Volume);

[InlineArray(10)]
public struct LevelBuffer
{
    private OrderBookLevel _element0;
}
