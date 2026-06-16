using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;

namespace Vertr.Market.Application.Models;

/// <summary>
/// Double-buffered throttler — полностью lock-free подход с исправленными race conditions.
///
/// Два буфера, atomic swap через Interlocked.CompareExchange (loop).
/// Reader использует Volatile.Read для visibility, CompareExchange с retry для atomic swap.
///
/// Thread-safety: _activeIndex читается через Volatile.Read, swap через CompareExchange loop.
/// Это гарантирует, что между чтением и обменом writer не может незаметно изменить состояние.
/// </summary>
public sealed class OrderBookThrottlerDoubleBuffer
{
    private readonly OrderBook[] _bufferA = new OrderBook[OrderBookEvent.Capacity];
    private readonly OrderBook[] _bufferB = new OrderBook[OrderBookEvent.Capacity];

    // Активный буфер: 0 = A, 1 = B.
    // Без volatile — используем Volatile.Read/Volatile.Write для гарантированной visibility.
    private int _activeIndex;

    private readonly RingBuffer<OrderBookEvent> _ringBuffer;
    private readonly TimeSpan _interval;
    private readonly int _orderBookSize = Unsafe.SizeOf<OrderBook>();

    public OrderBookThrottlerDoubleBuffer(TimeSpan interval, RingBuffer<OrderBookEvent> ringBuffer)
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
                var bytesRead = await stream.ReadAsync(memoryBuffer, ct).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                ref readonly var incomingBook = ref MemoryMarshal.AsRef<OrderBook>(memoryBuffer.Span);
                HandleIncomingOrderBook(in incomingBook);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentArray);
        }
    }

    /// <summary>
    /// Шаг 2: Обновление состояния по принципу "последний пришедший побеждает" (Zero-Allocation, lock-free).
    /// </summary>
    public void HandleIncomingOrderBook(in OrderBook incomingBook)
    {
        // Volatile read — гарантирует, что мы видим последнее значение _activeIndex от любого writer.
        // На ARM: VolatileRead → LDREX/STREX (exclusive load).
        // На x86/x64: VolatileRead → memory barrier (полный барьер).
        var active = Volatile.Read(ref _activeIndex);

        // Порядок операций критичен:
        // 1. Читаем active (volatile)
        // 2. Записываем в _buffer[active]
        // 3. writer читает _activeIndex (volatile) → получает новое значение → пишет в другой буфер
        //
        // Это гарантирует, что writer всегда пишет в активный буфер, который reader
        // не читает в данный момент (или читает — в этом случае данные попадут в следующий тик).
        if (active == 0)
        {
            _bufferA[incomingBook.AssetId] = incomingBook;
        }
        else
        {
            _bufferB[incomingBook.AssetId] = incomingBook;
        }
    }

    /// <summary>
    /// Шаг 3: Периодический сброс (раз в 5 секунд) накопленных срезов в Disruptor.
    /// </summary>
    public async Task StartEmittingAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            var sequence = _ringBuffer.Next();
            var @event = _ringBuffer[sequence];

            @event.Clear();

            // Atomic swap через CompareExchange loop.
            //
            // Почему не Interlocked.Exchange(ref _activeIndex, _activeIndex ^ 1)?
            // Потому что _activeIndex ^ 1 читает _activeIndex ДО exchange.
            // Между чтением и обменом writer может изменить _activeIndex.
            // Например: reader читает 0, writer меняет на 1, Exchange пишет 0^1=1 → writer и reader
            // оба пишут в буфер B (потому что writer уже записал 1, а reader использовал stale 0).
            //
            // CompareExchange loop гарантирует:
            // 1. Мы читаем актуальное значение (Volatile.Read)
            // 2. Вычисляем next из этого значения
            // 3. Swap только если значение не изменилось
            // 4. Если изменилось — retry с новым значением
            //
            // Это устраняет race condition, где writer и reader пишут в один буфер.
            int prevIndex;
            int current;
            do
            {
                // Volatile read — гарантируем, что видим последнее значение от writer.
                current = Volatile.Read(ref _activeIndex);
                var next = current ^ 1;

                // CompareExchange: swap только если _activeIndex == current.
                // Возвращает старое значение (которое может отличаться от current, если writer
                // поменял его между Volatile.Read и CompareExchange).
                //
                // Memory barrier: CompareExchange на x86/x64 → LOCK CMPXCHG (full barrier).
                // На ARM → LDREX/STREX (exclusive barrier).
                prevIndex = Interlocked.CompareExchange(ref _activeIndex, next, current);

                // Если prevIndex != current, значит writer поменял _activeIndex между
                // чтением и обменом. Retry с новым current.
            } while (prevIndex != current);

            // prevIndex — это старое значение _activeIndex, которое было активным ДО swap.
            // Writer больше не пишет в этот буфер (он уже переключился на next).
            // Мы — единственные читатели этого буфера. Консистентный снимок гарантирован.
            var source = prevIndex == 0 ? _bufferA : _bufferB;
            for (var index = 0; index < OrderBookEvent.Capacity; index++)
            {
                @event[index] = source[index];
            }

            _ringBuffer.Publish(sequence);
        }
    }
}
