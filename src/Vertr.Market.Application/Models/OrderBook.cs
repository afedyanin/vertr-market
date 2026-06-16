using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Vertr.Market.Application.Models;


// Основная структура стакана (размер: ~340 байт). Передается везде по ссылке (in / ref).
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

// Класс-контейнер события для Disruptor.
// Экземпляры создаются ОДИН РАЗ при старте внутри Ring Buffer и используются повторно.
// Pre-allocated array на всё время жизни — ноль аллокаций в steady-state.
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

    public ref OrderBook this[int index] => ref _books[index];

    public void CopyTo(OrderBookEvent destination)
    {
        Array.Copy(_books, destination._books, Capacity);
    }
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
