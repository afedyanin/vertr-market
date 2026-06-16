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

        if (Interlocked.CompareExchange(ref _parserStarted, 1, 0) == 1)
        {
            throw new InvalidOperationException("Parser already started.");
        }

        var reader = PipeReader.Create(stream, new StreamPipeReaderOptions(
            bufferSize: OrderBookSize * 8, // Увеличили для стабильности window
            minimumReadSize: OrderBookSize,
            leaveOpen: false)); // Измените на false, если управление жизненным циклом тут

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

            if (result.IsCanceled)
            {
                break;
            }

            var consumed = buffer.Start;

            while (buffer.Length >= OrderBookSize)
            {
                var orderBookBuffer = buffer.Slice(0, OrderBookSize);

                // Получаем sequence непосредственно перед работой, гарантируя атомарность для Disruptor
                var sequence = _ringBuffer.Next();
                try
                {
                    // Пишем НАПРЯМУЮ в RingBuffer без создания тяжелой структуры в стеке
                    ref var targetBook = ref _ringBuffer[sequence].OrderBook;
                    var destination = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref targetBook, 1));

                    orderBookBuffer.CopyTo(destination);
                }
                catch
                {
                    // В случае падения парсинга — публикуем пустой/сломанный ивент, 
                    // чтобы не повесить RingBuffer, либо обрабатываем иначе
                    throw;
                }
                finally
                {
                    _ringBuffer.Publish(sequence);
                }

                buffer = buffer.Slice(orderBookBuffer.End);
                consumed = orderBookBuffer.End;
            }

            reader.AdvanceTo(consumed, buffer.End);

            if (result.IsCompleted)
            {
                if (buffer.Length > 0)
                {
                    throw new EndOfStreamException("Stream ended with incomplete OrderBook data.");
                }

                break;
            }
        }
    }
}
