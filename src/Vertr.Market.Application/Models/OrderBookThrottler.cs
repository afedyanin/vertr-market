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

// Sequential + Pack = 1 гарантирует, что layout на диске/в сети совпадает с layout в памяти.
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct OrderBook
{
    public int AssetId;
    public long Timestamp; // Unix Timestamp

    public LevelBuffer Bids;
    public LevelBuffer Asks;

    public int BidCount;
    public int AskCount;
}

public sealed class OrderBookEvent
{
    private readonly OrderBook[] _books;

    public const int Capacity = 1024;

    public OrderBookEvent()
    {
        _books = new OrderBook[Capacity];
    }

    public void Clear()
    {
        Array.Clear(_books);
    }

    public ref readonly OrderBook this[int index] => ref _books[index];

    public void CopyTo(OrderBookEvent destination)
    {
        Array.Copy(_books, destination._books, Capacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index, in OrderBook book)
    {
        _books[index] = book;
    }
}

public readonly record struct OrderBookLevel(decimal Price, decimal Volume);

[InlineArray(10)]
public struct LevelBuffer
{
    private OrderBookLevel _element0;
}
