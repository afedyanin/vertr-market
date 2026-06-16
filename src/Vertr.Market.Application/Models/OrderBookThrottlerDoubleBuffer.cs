using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;

namespace Vertr.Market.Application.Models;

/// <summary>
/// Throttler для стаканов — читает бинарный поток и периодически публикует снимки в Disruptor.
///
/// HandleIncomingOrderBook (writer) и StartEmittingAsync (reader) работают из разных потоков.
/// Writer пишет в active-буфер, reader читает active и swap-ит через Volatile.Read/Write.
/// </summary>
public sealed class OrderBookThrottlerDoubleBuffer
{
    private readonly OrderBook[] _bufferA = new OrderBook[OrderBookEvent.Capacity];
    private readonly OrderBook[] _bufferB = new OrderBook[OrderBookEvent.Capacity];

    // Активный буфер: 0 = A, 1 = B.
    // Reader использует Volatile.Read для visibility writer-записей.
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
    /// Шаг 2: Обновление состояния по принципу "последний пришедший побеждает".
    /// Writer пишет в active-буфер, reader читает active и swap-ит.
    /// </summary>
    internal void HandleIncomingOrderBook(in OrderBook incomingBook)
    {
        var active = Volatile.Read(ref _activeIndex);
        var target = active == 0 ? _bufferA : _bufferB;
        target[incomingBook.AssetId] = incomingBook;
        Thread.MemoryBarrier();
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

            var active = Volatile.Read(ref _activeIndex);
            var source = active == 0 ? _bufferA : _bufferB;
            for (var index = 0; index < OrderBookEvent.Capacity; index++)
            {
                @event[index] = source[index];
            }

            Volatile.Write(ref _activeIndex, active ^ 1);

            _ringBuffer.Publish(sequence);
        }
    }
}
