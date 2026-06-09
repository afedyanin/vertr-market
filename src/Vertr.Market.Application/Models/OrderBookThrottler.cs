using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;

namespace Vertr.Market.Application.Models;

public sealed class OrderBookThrottler
{
    // Хранилище актуальных стаканов по ID актива. Память выделяется один раз при заполнении.
    // При поступлении новых данных ячейки памяти просто перезаписываются.
    private readonly Dictionary<int, OrderBook> _latestBooks = new(1024);
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
    /// Шаг 2: Обновление состояния по принципу "последний пришедший побеждает" (Zero-Allocation)
    /// </summary>
    public void HandleIncomingOrderBook(in OrderBook incomingBook)
    {
        ref var current = ref CollectionsMarshal.GetValueRefOrAddDefault(
            _latestBooks, incomingBook.AssetId, out var _);
        current = incomingBook;
    }

    /// <summary>
    /// Шаг 3: Периодический сброс (раз в 5 секунд) накопленных срезов в Disruptor
    /// </summary>
    public async Task StartEmittingAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            foreach (var kvp in _latestBooks)
            {
                var sequence = _ringBuffer.Next();
                var @event = _ringBuffer[sequence];
                @event.Value = kvp.Value;
                _ringBuffer.Publish(sequence);
            }
        }
    }
}
