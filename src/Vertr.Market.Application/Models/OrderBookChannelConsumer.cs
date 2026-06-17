using System.Threading.Channels;
using Disruptor;

namespace Vertr.Market.Application.Models;

public sealed class OrderBookChannelConsumer
{
    private readonly RingBuffer<OrderBookEvent> _ringBuffer;
    private readonly ChannelReader<OrderBook> _reader;

    public OrderBookChannelConsumer(RingBuffer<OrderBookEvent> ringBuffer, ChannelReader<OrderBook> reader)
    {
        _ringBuffer = ringBuffer;
        _reader = reader;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var orderBook in _reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                var sequence = _ringBuffer.Next();
                var eventSlot = _ringBuffer[sequence];
                eventSlot.OrderBook = orderBook;
                _ringBuffer.Publish(sequence);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
