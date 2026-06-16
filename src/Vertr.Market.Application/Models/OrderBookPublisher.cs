using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;

namespace Vertr.Market.Application.Models;

public sealed class OrderBookPublisher
{
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
            bufferSize: 8192, // Оптимальный размер буфера для минимизации системных вызовов
            minimumReadSize: OrderBookSize,
            leaveOpen: false));

        Exception? error = null;

        try
        {
            await ParsePipeAsync(reader, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            error = ex;
            throw;
        }
        finally
        {
            await reader.CompleteAsync(error).ConfigureAwait(false);
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
                reader.AdvanceTo(buffer.Start, buffer.Start);
                break;
            }

            var consumed = buffer.Start;
            var examined = buffer.Start;

            try
            {
                var seqReader = new SequenceReader<byte>(buffer);

                while (seqReader.Remaining >= OrderBookSize)
                {
                    var sequence = _ringBuffer.Next();
                    var eventSlot = _ringBuffer[sequence];

                    var slotSpan = MemoryMarshal.CreateSpan(ref eventSlot.OrderBook, 1);
                    var destination = MemoryMarshal.AsBytes(slotSpan);

                    if (!seqReader.TryCopyTo(destination))
                    {
                        eventSlot.IsValid = false;
                        _ringBuffer.Publish(sequence);
                        throw new InvalidDataException("Failed to copy data from sequence reader.");
                    }

                    seqReader.Advance(OrderBookSize);
                    eventSlot.IsValid = true;
                    _ringBuffer.Publish(sequence);
                }

                consumed = seqReader.Position;
                examined = seqReader.Remaining > 0 ? buffer.End : consumed;
            }
            finally
            {
                reader.AdvanceTo(consumed, examined);
            }

            if (result.IsCompleted)
            {
                var remaining = buffer.Slice(consumed).Length;
                if (remaining > 0)
                {
                    throw new EndOfStreamException($"Stream ended with incomplete OrderBook data. Remainder: {remaining} bytes.");
                }

                break;
            }
        }
    }
}
