using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;

namespace Vertr.Market.Application.Models;

public sealed class OrderBookThrottler
{
    private readonly object _lock = new();
    private readonly RingBuffer<OrderBookEvent> _ringBuffer;
    private readonly TimeSpan _interval;
    private readonly int _orderBookSize = Unsafe.SizeOf<OrderBook>();
    private long _sequence;

    public OrderBookThrottler(TimeSpan interval, RingBuffer<OrderBookEvent> ringBuffer)
    {
        _interval = interval;
        _ringBuffer = ringBuffer;

        // Init first event
        _sequence = _ringBuffer.Next();
        _ringBuffer[_sequence].Clear();
    }

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

    private void HandleIncomingOrderBook(in OrderBook incomingBook)
    {
        lock (_lock)
        {
            _ringBuffer[_sequence][incomingBook.AssetId] = incomingBook;
        }
    }

    public async Task StartEmittingAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            lock (_lock)
            {
                _ringBuffer.Publish(_sequence);

                // Init next event
                _sequence = _ringBuffer.Next();
                _ringBuffer[_sequence].Clear();
            }
        }
    }
}
