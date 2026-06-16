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

        var buffer = new byte[OrderBookSize];
        var memoryBuffer = buffer.AsMemory();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await stream.ReadExactlyAsync(memoryBuffer, ct).ConfigureAwait(false);

                // Проверяем отмену перед записью — экономим work при cancellation.
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                ref readonly var incomingBook = ref MemoryMarshal.AsRef<OrderBook>(buffer);

                if (incomingBook.AssetId < 0 || (uint)incomingBook.AssetId >= OrderBookEvent.Capacity)
                {
                    continue;
                }

                // Защищаем неатомарное копирование структуры в аккумулятор
                lock (_syncLock)
                {
                    _accumulator[incomingBook.AssetId] = incomingBook;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Нормальное завершение работы
        }
        catch (IOException) when (ct.IsCancellationRequested)
        {
            // Сеть разорвана из-за отмены — не выбрасываем повторно
        }
        catch (ObjectDisposedException) when (ct.IsCancellationRequested)
        {
            // Stream был удалён из-за отмены — не выбрасываем повторно
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
                var sequence = _ringBuffer.Next();

                try
                {
                    var targetEvent = _ringBuffer[sequence];

                    lock (_syncLock)
                    {
                        _accumulator.CopyTo(targetEvent);
                    }
                }
                finally
                {
                    _ringBuffer.Publish(sequence);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Нормальное завершение при отмене таймера
        }
    }
}
