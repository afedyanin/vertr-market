using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;

namespace Vertr.Market.Application.Models;

public sealed class OrderBookThrottler
{
    // Хранилище актуальных стаканов по индексу AssetId. Память выделяется один раз при заполнении.
    // При поступлении новых данных ячейки памяти просто перезаписываются.
    //
    // Thread-safety: _latestBooks читается в StartEmittingAsync (поток таймера) и записывается
    // в HandleIncomingOrderBook (поток парсера). Lock гарантирует, что emitter видит
    // консистентный снимок — либо все предыдущие записи, либо все новые, но никогда
    // промежуточное состояние, когда часть элементов обновлена, а часть — нет.
    private readonly OrderBook[] _latestBooks = new OrderBook[OrderBookEvent.Capacity];
    private readonly object _lock = new();

    private readonly RingBuffer<OrderBookEvent> _ringBuffer;
    private readonly TimeSpan _interval;
    private readonly int _orderBookSize = Unsafe.SizeOf<OrderBook>();

    public OrderBookThrottler(TimeSpan interval, RingBuffer<OrderBookEvent> ringBuffer)
    {
        _interval = interval;
        _ringBuffer = ringBuffer;
    }

    /// <summary>
    /// Шаг 1: Быстрое чтение стаканов из бинарного стрима без аллокаций.
    /// </summary>
    public async ValueTask ParseStreamAsync(Stream stream, CancellationToken ct)
    {
        var rentArray = ArrayPool<byte>.Shared.Rent(_orderBookSize);
        var memoryBuffer = rentArray.AsMemory(0, _orderBookSize);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await stream.ReadExactlyAsync(memoryBuffer, ct).ConfigureAwait(false);
                ref readonly var incomingBook = ref MemoryMarshal.AsRef<OrderBook>(memoryBuffer.Span);

                if ((uint)incomingBook.AssetId >= OrderBookEvent.Capacity)
                {
                    // Логируем ошибку / пропускаем коррумпированный пакет
                    continue;
                }

                HandleIncomingOrderBook(in incomingBook);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentArray);
        }
    }

    /// <summary>
    /// Шаг 2: Обновление состояния по принципу "последний пришедший побеждает" (Zero-Allocation)
    /// </summary>
    public void HandleIncomingOrderBook(in OrderBook incomingBook)
    {
        lock (_lock)
        {
            _latestBooks[incomingBook.AssetId] = incomingBook;
        }
    }

    /// <summary>
    /// Шаг 3: Периодический сброс накопленных срезов в Disruptor
    /// </summary>
    public async Task StartEmittingAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            var sequence = _ringBuffer.Next();
            var @event = _ringBuffer[sequence];

            @event.Clear();

            lock (_lock)
            {
                for (var index = 0; index < OrderBookEvent.Capacity; index++)
                {
                    @event[index] = _latestBooks[index];
                }
            }

            _ringBuffer.Publish(sequence);
        }
    }
}
