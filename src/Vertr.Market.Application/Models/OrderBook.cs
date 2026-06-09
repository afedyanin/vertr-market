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
public sealed class OrderBookEvent
{
    public OrderBook Value;
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
