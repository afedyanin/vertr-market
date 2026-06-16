using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;

namespace Vertr.Market.Application.Models;

public sealed class OrderBookPublisher
{
    // ВНИМАНИЕ: Убедитесь, что бинарный размер структуры в потоке ТОЧНО равен Unsafe.SizeOf
    private static readonly int OrderBookSize = Unsafe.SizeOf<OrderBook>();
    private readonly RingBuffer<OrderBookEvent> _ringBuffer;
    private int _parserStarted;

    public OrderBookPublisher(RingBuffer<OrderBookEvent> ringBuffer)
    {
        ArgumentNullException.ThrowIfNull(ringBuffer);
        _ringBuffer = ringBuffer;
    }

    public async Task ParseStreamAsync(Stream stream, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (Interlocked.CompareExchange(ref _parserStarted, 1, 0) != 0)
        {
            throw new InvalidOperationException("Parser already started.");
        }

        var reader = PipeReader.Create(stream, new StreamPipeReaderOptions(
            bufferSize: OrderBookSize * 8,
            minimumReadSize: OrderBookSize,
            leaveOpen: false));

        try
        {
            await ParsePipeAsync(reader, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
            Interlocked.Exchange(ref _parserStarted, 0);
        }
    }

    private async Task ParsePipeAsync(PipeReader reader, CancellationToken ct)
    {
        while (true)
        {
            var result = await reader.ReadAsync(ct).ConfigureAwait(false);
            var buffer = result.Buffer;

            if (result.IsCanceled)
            {
                break;
            }

            var consumed = buffer.Start;
            var examined = buffer.Start;

            try
            {
                while (buffer.Length >= OrderBookSize)
                {
                    var orderBookBuffer = buffer.Slice(0, OrderBookSize);

                    var sequence = _ringBuffer.Next();
                    var eventSlot = _ringBuffer[sequence];

                    try
                    {
                        ref var targetBook = ref eventSlot.OrderBook;
                        var destination = MemoryMarshal.CreateSpan(ref Unsafe.As<OrderBook, byte>(ref targetBook), OrderBookSize);

                        if (orderBookBuffer.FirstSpan.Length >= OrderBookSize)
                        {
                            orderBookBuffer.FirstSpan.Slice(0, OrderBookSize).CopyTo(destination);
                        }
                        else
                        {
                            orderBookBuffer.CopyTo(destination);
                        }

                        eventSlot.IsValid = true;
                    }
                    catch
                    {
                        eventSlot.IsValid = false;
                        _ringBuffer.Publish(sequence);
                        throw;
                    }

                    _ringBuffer.Publish(sequence);

                    buffer = buffer.Slice(orderBookBuffer.End);
                    consumed = orderBookBuffer.End;
                }

                examined = buffer.Length > 0 ? buffer.End : consumed;
            }
            finally
            {
                reader.AdvanceTo(consumed, examined);
            }

            if (result.IsCompleted)
            {
                if (buffer.Length > 0)
                {
                    throw new EndOfStreamException($"Stream ended with incomplete OrderBook data. Remainder: {buffer.Length} bytes.");
                }

                break;
            }
        }
    }
}
