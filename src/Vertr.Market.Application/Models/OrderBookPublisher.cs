using System.Buffers;
using System.IO.Pipelines;
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

    private int _parserStarted;

    public OrderBookPublisher(RingBuffer<OrderBookEvent> ringBuffer, ILogger<OrderBookPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(ringBuffer);
        ArgumentNullException.ThrowIfNull(logger);
        _ringBuffer = ringBuffer;
        _logger = logger;
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in OrderBook PipeReader loop");
            throw;
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
            // Асинхронно ждем появления данных в пайплайне
            var result = await reader.ReadAsync(ct).ConfigureAwait(false);
            var buffer = result.Buffer;

            // Обрабатываем все полные структуры OrderBook, которые сейчас есть в буфере
            while (buffer.Length >= OrderBookSize)
            {
                // Выделяем ровно тот кусок памяти, который занимает одна структура
                var orderBookBuffer = buffer.Slice(0, OrderBookSize);

                OrderBook incomingBook;

                // Если буфер непрерывен в памяти (fast path), читаем структуру напрямую
                if (orderBookBuffer.IsSingleSegment)
                {
                    incomingBook = MemoryMarshal.Read<OrderBook>(orderBookBuffer.First.Span);
                }
                else
                {
                    // Если буфер разбит на сегменты (slow path, бывает редко), копируем на стек
                    Unsafe.SkipInit(out incomingBook);
                    var destination = MemoryMarshal.CreateSpan(ref Unsafe.As<OrderBook, byte>(ref incomingBook), OrderBookSize);
                    orderBookBuffer.CopyTo(destination);
                }

                // Публикуем в Disruptor RingBuffer
                var sequence = _ringBuffer.Next();
                try
                {
                    _ringBuffer[sequence].OrderBook = incomingBook;
                }
                finally
                {
                    _ringBuffer.Publish(sequence);
                }

                // Сдвигаем курсор буфера вперед на размер прочитанной структуры
                buffer = buffer.Slice(orderBookBuffer.End);
            }

            // Говорим PipeReader, сколько данных мы потребили (consumed), 
            // и до какого момента исследовали буфер (examined)
            reader.AdvanceTo(buffer.Start, buffer.End);

            // Если стрим завершился и данных больше не будет
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

[StructLayout(LayoutKind.Sequential)]
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
    public OrderBook OrderBook;
}

public readonly record struct OrderBookLevel(decimal Price, decimal Volume);

[InlineArray(10)]
public struct LevelBuffer
{
    private OrderBookLevel _element0;
}
