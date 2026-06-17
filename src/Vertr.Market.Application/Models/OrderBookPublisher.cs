using System.Threading.Channels;
using Disruptor;

namespace Vertr.Market.Application.Models;

public sealed class OrderBookPublisher
{
    private readonly RingBuffer<OrderBookEvent> _ringBuffer;
    private int _parserStarted;

    public OrderBookPublisher(RingBuffer<OrderBookEvent> ringBuffer)
    {
        ArgumentNullException.ThrowIfNull(ringBuffer);
        _ringBuffer = ringBuffer;
    }

    public async Task ParseChannelReaderAsync(ChannelReader<OrderBook> reader, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (Interlocked.CompareExchange(ref _parserStarted, 1, 0) != 0)
        {
            throw new InvalidOperationException("Parser already started.");
        }

        try
        {
            await foreach (var orderBook in reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                var sequence = _ringBuffer.Next();
                var eventSlot = _ringBuffer[sequence];

                eventSlot.OrderBook = orderBook;
                eventSlot.IsValid = true;

                _ringBuffer.Publish(sequence);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            Interlocked.Exchange(ref _parserStarted, 0);
            throw;
        }
        finally
        {
            Interlocked.Exchange(ref _parserStarted, 0);
        }
    }
}
