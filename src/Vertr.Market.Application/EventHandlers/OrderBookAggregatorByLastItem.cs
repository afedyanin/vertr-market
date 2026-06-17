using System.Buffers;
using System.Runtime.CompilerServices;
using Disruptor;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.EventHandlers;

public class OrderBookAggregatorByLastItem : IEventHandler<OrderBookEvent>
{
    private readonly TimeSpan _interval;
    private readonly IOrderBookSnapshotPublisher _publisher;

    private readonly Dictionary<int, OrderBook> _bufferA;
    private readonly Dictionary<int, OrderBook> _bufferB;

    private volatile Dictionary<int, OrderBook> _current;
    private volatile Dictionary<int, OrderBook> _snapshot;

    public OrderBookAggregatorByLastItem(
        IOrderBookSnapshotPublisher publisher,
        TimeSpan interval,
        int capacity = 1024)
    {
        _publisher = publisher;
        _interval = interval;

        _bufferA = new Dictionary<int, OrderBook>(capacity);
        _bufferB = new Dictionary<int, OrderBook>(capacity);

        _current = _bufferA;
        _snapshot = _bufferB;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnEvent(ref readonly OrderBookEvent data, long sequence, bool endOfBatch)
    {
        // Извлекаем прямую ссылку на структуру внутри слота RingBuffer (0 копирований)
        ref readonly var book = ref data.OrderBook;

        // Копирование происходит один раз — непосредственно при записи в хэш-таблицу
        _current[book.AssetId] = book;
    }

    /// <summary>
    /// Стандартная реализация интерфейса Disruptor.
    /// Перенаправляет вызов в оптимизированный метод. Полностью стирается JIT-компилятором при инлайнинге.
    /// </summary>
    void IEventHandler<OrderBookEvent>.OnEvent(OrderBookEvent data, long sequence, bool endOfBatch)
    {
        OnEvent(in data, sequence, endOfBatch);
    }

    /// <summary>
    /// Асинхронный цикл публикации снимков стаканов. Вызывается в фоновом потоке.
    /// </summary>
    public async Task StartEmittingAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(ct))
        {
            // Атомарная и потокобезопасная подмена активного буфера (за счет volatile и Interlocked)
            var snapshot = Interlocked.Exchange(ref _current, _snapshot);
            _snapshot = snapshot;

            var count = _snapshot.Count;
            if (count == 0)
            {
                continue;
            }

            // Арендуем массив структур из пула .NET во избежание аллокаций в куче (heap)
            var rentedArray = ArrayPool<OrderBook>.Shared.Rent(count);

            try
            {
                // Копируем значения из словаря в арендованный массив
                _snapshot.Values.CopyTo(rentedArray, 0);

                // Моментально очищаем буфер, подготавливая его к приему данных в следующем цикле
                _snapshot.Clear();

                // Публикуем срез памяти без аллокаций через ReadOnlyMemory
                await _publisher.PublishAsync(new ReadOnlyMemory<OrderBook>(rentedArray, 0, count), ct);
            }
            finally
            {
                // Обязательный возврат массива в пул
                ArrayPool<OrderBook>.Shared.Return(rentedArray);
            }
        }
    }
}

public interface IOrderBookSnapshotPublisher
{
    // ReadOnlyMemory обеспечивает Zero-Allocation и совместим с async/await конструкциями
    Task PublishAsync(ReadOnlyMemory<OrderBook> books, CancellationToken ct);
}
