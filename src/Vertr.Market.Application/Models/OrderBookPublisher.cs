using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;
using Microsoft.Extensions.Logging;

namespace Vertr.Market.Application.Models;

public sealed class OrderBookPublisher
{
    private static readonly int OrderBookSize = Unsafe.SizeOf<OrderBook>();
    private readonly RingBuffer<OrderBookEvent> _ringBuffer;
    private readonly ILogger<OrderBookPublisher> _logger;

    private static int _parserStarted;

    public OrderBookPublisher(RingBuffer<OrderBookEvent> ringBuffer, ILogger<OrderBookPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(ringBuffer);
        ArgumentNullException.ThrowIfNull(logger);
        _ringBuffer = ringBuffer;
        _logger = logger;
    }

    public Task StartParsingAsync(Stream stream, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (Interlocked.CompareExchange(ref _parserStarted, 1, 0) == 1)
        {
            throw new InvalidOperationException("Parser already started.");
        }

        return Task.Factory.StartNew(
            () => ParseStreamSyncLoop(stream, ct),
            ct,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private void ParseStreamSyncLoop(Stream stream, CancellationToken ct)
    {
        using var registration = ct.Register(() =>
        {
            try
            {
                stream.Close();
            }
            catch { }
        });

        var rentBuffer = ArrayPool<byte>.Shared.Rent(OrderBookSize);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var bytesRead = 0;
                var validSpan = rentBuffer.AsSpan(0, OrderBookSize);

                while (bytesRead < OrderBookSize)
                {
                    var read = stream.Read(validSpan.Slice(bytesRead));
                    if (read == 0)
                    {
                        throw new EndOfStreamException("Stream ended unexpectedly.");
                    }

                    bytesRead += read;
                }

                ref readonly var incomingBook = ref MemoryMarshal.AsRef<OrderBook>(validSpan);

                var sequence = _ringBuffer.Next();
                try
                {
                    var targetEvent = _ringBuffer[sequence];
                    targetEvent.Update(in incomingBook);
                }
                finally
                {
                    _ringBuffer.Publish(sequence);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ожидаемое завершение при отмене токена.
        }
        catch (IOException)
        {
            // Ожидаемое завершение при закрытии/разрыве stream.
        }
        catch (ObjectDisposedException)
        {
            // Ожидаемое завершение при dispose stream.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in ParseStreamSyncLoop");
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentBuffer);
            Volatile.Write(ref _parserStarted, 0);
        }
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct OrderBook
{
    public int AssetId;
    public long Timestamp;
    public LevelBuffer Bids;
    public LevelBuffer Asks;
    public int BidCount;
    public int AskCount;
}

public sealed class OrderBookEvent
{
    private OrderBook _orderBook;
    private int _version;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(in OrderBook newBook)
    {
        Volatile.Write(ref _version, _version + 1);
        Thread.MemoryBarrier();

        _orderBook = newBook;

        Volatile.Write(ref _version, _version + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead(out OrderBook result)
    {
        var spinWait = new SpinWait();

        for (var i = 0; i < 30; i++)
        {
            var startVersion = Volatile.Read(ref _version);

            // Нечётная версия означает, что запись идёт.
            if ((startVersion & 1) != 0)
            {
                spinWait.SpinOnce();
                continue;
            }

            Thread.MemoryBarrier();
            result = _orderBook;
            Thread.MemoryBarrier();

            if (startVersion == Volatile.Read(ref _version))
            {
                return true;
            }

            spinWait.SpinOnce();
        }

        result = default;
        return false;
    }
}

public readonly record struct OrderBookLevel(decimal Price, decimal Volume);

[InlineArray(10)]
public struct LevelBuffer
{
    private OrderBookLevel _element0;
}
