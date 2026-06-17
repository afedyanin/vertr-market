using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Disruptor;

namespace Vertr.Market.Application.Models;

public sealed class OrderBookPublisher
{
    private readonly RingBuffer<OrderBookEvent> _ringBuffer;
    private int _parserStarted;

    private const int MaxBatchSize = 32;

    public OrderBookPublisher(RingBuffer<OrderBookEvent> ringBuffer)
    {
        _ringBuffer = ringBuffer;
    }

    public async Task ParseChannelReaderAsync(ChannelReader<OrderBook> reader, CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _parserStarted, 1, 0) != 0)
        {
            throw new InvalidOperationException("Parser already started.");
        }

        try
        {
            while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (reader.Count > 0) // Быстрая проверка наличия элементов
                {
                    var batchSize = Math.Min(reader.Count, MaxBatchSize);

                    if (batchSize == 0)
                    {
                        batchSize = 1;
                    }

                    var hi = _ringBuffer.Next(batchSize);
                    var lo = hi - batchSize + 1;

                    try
                    {
                        for (var sequence = lo; sequence <= hi; sequence++)
                        {
                            if (reader.TryRead(out var orderBook))
                            {
                                _ringBuffer[sequence].OrderBook = orderBook;
                            }
                            else
                            {
                                hi = sequence - 1;
                                break;
                            }
                        }
                    }
                    finally
                    {
                        if (hi >= lo)
                        {
                            _ringBuffer.Publish(lo, hi);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Interlocked.Exchange(ref _parserStarted, 0);
        }
    }
}

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

public readonly record struct OrderBookLevel(long Price, long Volume);

[InlineArray(10)]
public struct LevelBuffer
{
    private OrderBookLevel _element0;
}
