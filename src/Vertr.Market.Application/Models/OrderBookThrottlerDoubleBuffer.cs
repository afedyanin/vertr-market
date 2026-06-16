using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;

namespace Vertr.Market.Application.Models;

/// <summary>
/// Throttler for order books — reads a binary stream and periodically publishes snapshots to the Disruptor.
///
/// HandleIncomingOrderBook (writer) and StartEmittingAsync (reader) run on different threads.
/// Writer writes to the active buffer; reader copies and flips via Interlocked.Exchange.
/// </summary>
public sealed class OrderBookThrottlerDoubleBuffer
{
    private readonly OrderBook[] _bufferA = new OrderBook[OrderBookEvent.Capacity];
    private readonly OrderBook[] _bufferB = new OrderBook[OrderBookEvent.Capacity];

    // Active buffer: 0 = A, 1 = B.
    // Reader uses Interlocked.Exchange for atomic flip with full memory barrier.
    private int _activeIndex;

    private readonly RingBuffer<OrderBookEvent> _ringBuffer;
    private readonly TimeSpan _interval;
    private readonly int _orderBookStructSize = Unsafe.SizeOf<OrderBook>();

    public OrderBookThrottlerDoubleBuffer(TimeSpan interval, RingBuffer<OrderBookEvent> ringBuffer)
    {
        ArgumentNullException.ThrowIfNull(ringBuffer);
        _interval = interval;
        _ringBuffer = ringBuffer;
    }

    /// <summary>
    /// Step 1: Fast reading of order books from the binary stream with zero allocations.
    /// </summary>
    public async ValueTask ParseStreamAsync(Stream stream, CancellationToken ct)
    {
        // Binary stream layout must match the in-memory layout of OrderBook.
        // OrderBook is a struct with value-type fields (LevelBuffer uses inline arrays),
        // so sizeof(OrderBook) reflects the raw binary size.
        var rentArray = ArrayPool<byte>.Shared.Rent(_orderBookStructSize);
        var memoryBuffer = rentArray.AsMemory(0, _orderBookStructSize);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await stream.ReadExactlyAsync(memoryBuffer, ct).ConfigureAwait(false);
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
    /// Step 2: Last-writer-wins state update.
    /// Writer writes to the active buffer and does NOT flip _activeIndex — flip is the reader's responsibility.
    /// In the classic double-buffer pattern, writer writes to active, reader copies active and flips the index.
    /// If writer flips the index itself, writer and reader end up sharing one buffer — double-buffering is lost.
    /// </summary>
    internal void HandleIncomingOrderBook(in OrderBook incomingBook)
    {
        var active = Volatile.Read(ref _activeIndex);
        var target = active == 0 ? _bufferA : _bufferB;
        target[incomingBook.AssetId] = incomingBook;
    }

    /// <summary>
    /// Step 3: Periodic flush of accumulated snapshots to the Disruptor.
    ///
    /// Critical: flip happens BEFORE copying.
    /// If we copy first then flip, writer continues writing to the active buffer during the entire for-loop,
    /// resulting in an inconsistent snapshot: some data is "old", some is "new".
    /// Flip before copy guarantees: writer switches to the other buffer, making the active buffer
    /// "frozen" for safe reading.
    /// </summary>
    public async Task StartEmittingAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            var sequence = _ringBuffer.Next();
            var @event = _ringBuffer[sequence];

            @event.Clear();

            // 1) Flip index before copying — writer cannot interleave (only reader writes _activeIndex).
            var active = Volatile.Read(ref _activeIndex);
            Volatile.Write(ref _activeIndex, active ^ 1);

            // 2) Copy the buffer that was active before the flip (writer cannot touch it now).
            var source = active == 0 ? _bufferA : _bufferB;
            for (var index = 0; index < OrderBookEvent.Capacity; index++)
            {
                @event[index] = source[index];
            }

            _ringBuffer.Publish(sequence);
        }
    }
}
