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

        try
        {
            await ParsePipeAsync(reader, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Ожидаемое завершение при отмене токена
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
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
                    // Сначала берем слот. Если Disruptor перегружен, поток заблокируется тут.
                    var sequence = _ringBuffer.Next();

                    try
                    {
                        var eventSlot = _ringBuffer[sequence];
                        OrderBook localBook = default;

                        var destination = MemoryMarshal.CreateSpan(
                            ref Unsafe.As<OrderBook, byte>(ref localBook),
                            OrderBookSize);

                        if (!seqReader.TryCopyTo(destination))
                        {
                            throw new InvalidDataException("Failed to copy data from sequence reader.");
                        }

                        seqReader.Advance(OrderBookSize);

                        eventSlot.OrderBook = localBook;
                        eventSlot.IsValid = true;
                    }
                    catch
                    {
                        // Ошибка внутри конкретного слота — не публикуем инвалидный шаг в продакшн, 
                        // а даем упасть всему пайплайну маркет-даты (Fail-Fast).
                        var eventSlot = _ringBuffer[sequence];
                        eventSlot.IsValid = false;
                        _ringBuffer.Publish(sequence);
                        throw;
                    }

                    // Публикуем только при успешном заполнении слота
                    _ringBuffer.Publish(sequence);
                }

                consumed = seqReader.Position;
                // Если остались байты, изучаем буфер до конца (ждём дочитки)
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
