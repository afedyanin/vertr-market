using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;

namespace Vertr.Market.Application.Models;

public sealed class OrderBookThrottler
{
    private static readonly int OrderBookSize = Unsafe.SizeOf<OrderBook>();
    private readonly RingBuffer<OrderBookEvent> _ringBuffer;
    private readonly TimeSpan _interval;

    private readonly OrderBookEvent _accumulator = new();
    private readonly object _syncLock = new();

    private int _parserStarted;
    private int _emitterStarted;

    public OrderBookThrottler(TimeSpan interval, RingBuffer<OrderBookEvent> ringBuffer)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(ringBuffer);

        _interval = interval;
        _ringBuffer = ringBuffer;
    }

    public async ValueTask ParseStreamAsync(Stream stream, CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _parserStarted, 1, 0) == 1)
        {
            throw new InvalidOperationException("ParseStreamAsync already started.");
        }

        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new InvalidOperationException("Stream does not support reading.");
        }

        // Арендуем массив из пуста .NET (Zero allocation в рантайме)
        var rentBuffer = ArrayPool<byte>.Shared.Rent(OrderBookSize);
        var memoryBuffer = rentBuffer.AsMemory(0, OrderBookSize);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await stream.ReadExactlyAsync(memoryBuffer, ct).ConfigureAwait(false);
                ref readonly var incomingBook = ref MemoryMarshal.AsRef<OrderBook>(rentBuffer.AsSpan(0, OrderBookSize));

                if (incomingBook.AssetId < 0 || (uint)incomingBook.AssetId >= OrderBookEvent.Capacity)
                {
                    continue;
                }

                lock (_syncLock)
                {
                    _accumulator.Set(incomingBook.AssetId, in incomingBook);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) when (ct.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (ct.IsCancellationRequested) { }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentBuffer);
        }
    }

    public async Task StartEmittingAsync(CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _emitterStarted, 1, 0) == 1)
        {
            throw new InvalidOperationException("StartEmittingAsync already started.");
        }

        using var timer = new PeriodicTimer(_interval);

        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                long sequence = -1;
                try
                {
                    sequence = _ringBuffer.Next();
                    var targetEvent = _ringBuffer[sequence];

                    lock (_syncLock)
                    {
                        _accumulator.CopyTo(targetEvent);
                    }
                }
                finally
                {
                    if (sequence != -1)
                    {
                        _ringBuffer.Publish(sequence);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}