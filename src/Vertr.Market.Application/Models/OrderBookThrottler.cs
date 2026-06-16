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

        var localBuffer = GC.AllocateArray<byte>(OrderBookSize, pinned: true);
        var memoryBuffer = localBuffer.AsMemory(0, OrderBookSize);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await stream.ReadExactlyAsync(memoryBuffer, ct).ConfigureAwait(false);

                var validSpan = localBuffer.AsSpan(0, OrderBookSize);
                ref readonly var incomingBook = ref MemoryMarshal.AsRef<OrderBook>(validSpan);

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
                var isCopySuccessful = false;

                try
                {
                    sequence = _ringBuffer.Next();
                    var targetEvent = _ringBuffer[sequence];

                    lock (_syncLock)
                    {
                        _accumulator.CopyTo(targetEvent);
                    }

                    isCopySuccessful = true; // Фиксируем, что данные скопированы без ошибок
                }
                finally
                {
                    if (sequence != -1)
                    {
                        // Если копия сорвалась, затираем событие перед публикацией,
                        // чтобы консьюмеры Disruptor не обработали мусор.
                        if (!isCopySuccessful)
                        {
                            try
                            {
                                _ringBuffer[sequence].Clear();
                            }
                            catch { /* Игнорируем сопутствующие сбои */ }
                        }

                        // Контракт Disruptor: вызванный Next обязан завершиться Publish
                        _ringBuffer.Publish(sequence);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}