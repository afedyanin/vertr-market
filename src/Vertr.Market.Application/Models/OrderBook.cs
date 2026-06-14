using System.Runtime.CompilerServices;

namespace Vertr.Market.Application.Models;

// Основная структура стакана (размер: ~340 байт). Передается везде по ссылке (in / ref).
public struct OrderBook
{
    public int AssetId;
    public DateTime Timestamp;

    public LevelBuffer Bids;
    public LevelBuffer Asks;

    public int BidCount;
    public int AskCount;
}

// Класс-контейнер события для Disruptor.
// Экземпляры создаются ОДИН РАЗ при старте внутри Ring Buffer и используются повторно.
// Pre-allocated array на всё время жизни — ноль аллокаций в steady-state.
public sealed class OrderBookEvent
{
    private readonly OrderBook[] _books;
    public int Count { get; internal set; }

    public OrderBookEvent(int capacity)
    {
        _books = new OrderBook[capacity];
    }

    public void Clear()
    {
        if (Count > 0)
        {
            Array.Clear(_books, 0, Count);
            Count = 0;
        }
    }

    public ref OrderBook this[int index] => ref _books[index];
    public int Capacity => _books.Length;
}

// Элемент стакана (размер: 16 байт)
public readonly record struct OrderBookLevel(decimal Price, decimal Volume);

// Встроенный массив C# 12 (Inline Array) на 10 элементов.
// Предотвращает аллокацию массива в куче. Память выделяется прямо внутри структуры.
[InlineArray(10)]
public struct LevelBuffer
{
    private OrderBookLevel _element0;
}
