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

    public async Task StartParsingAsync(Stream stream, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (Interlocked.CompareExchange(ref _parserStarted, 1, 0) == 1)
        {
            throw new InvalidOperationException("Parser already started.");
        }

        // Оборачиваем поток в PipeReader с оптимальными настройками для парсинга структур
        var reader = PipeReader.Create(stream, new StreamPipeReaderOptions(
            bufferSize: OrderBookSize * 4, // Оптимальный размер буфера под несколько структур
            minimumReadSize: OrderBookSize,
            leaveOpen: false));

        try
        {
            await ParsePipeAsync(reader, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Ожидаемое завершение
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
            Interlocked.Exchange(ref _parserStarted, 0);
        }
    }

    private async Task ParsePipeAsync(PipeReader reader, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var result = await reader.ReadAsync(ct).ConfigureAwait(false);
            var buffer = result.Buffer;

            // Если чтение было отменено извне через CancelPendingRead
            if (result.IsCanceled)
            {
                break;
            }

            // Фиксируем начальную точку. Все, что мы успешно распарсим, 
            // сдвинет эту точку вперед.
            var consumed = buffer.Start;

            while (buffer.Length >= OrderBookSize)
            {
                var orderBookBuffer = buffer.Slice(0, OrderBookSize);
                OrderBook incomingBook;

                if (orderBookBuffer.IsSingleSegment)
                {
                    // Гарантируем, что Span имеет размер строго OrderBookSize
                    incomingBook = MemoryMarshal.Read<OrderBook>(orderBookBuffer.First.Span[..OrderBookSize]);
                }
                else
                {
                    Unsafe.SkipInit(out incomingBook);
                    var destination = MemoryMarshal.CreateSpan(ref Unsafe.As<OrderBook, byte>(ref incomingBook), OrderBookSize);
                    orderBookBuffer.CopyTo(destination);
                }

                var sequence = _ringBuffer.Next();
                try
                {
                    _ringBuffer[sequence].OrderBook = incomingBook;
                }
                finally
                {
                    _ringBuffer.Publish(sequence);
                }

                // Сдвигаем буфер для следующей итерации цикла
                buffer = buffer.Slice(orderBookBuffer.End);

                // Фиксируем, что эти данные мы ПОЛНОСТЬЮ потребили
                consumed = orderBookBuffer.End;
            }

            // Consumed: данные, которые ушли в Disruptor (их можно удалить из памяти)
            // Examined: данные, которые мы просмотрели полностью (включая недоеденный хвост buffer.End)
            reader.AdvanceTo(consumed, buffer.End);

            if (result.IsCompleted)
            {
                // buffer теперь содержит только недоеденный хвост
                if (buffer.Length > 0)
                {
                    throw new EndOfStreamException("Stream ended with incomplete OrderBook data.");
                }

                break;
            }
        }
    }
}
