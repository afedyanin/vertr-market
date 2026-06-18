using System.Threading.Channels;
using Disruptor;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.Consumers;

public sealed class TradeChannelConsumer
{
    private readonly RingBuffer<TradeEvent> _ringBuffer;
    private readonly ChannelReader<Trade> _reader;

    public TradeChannelConsumer(RingBuffer<TradeEvent> ringBuffer, ChannelReader<Trade> reader)
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
                eventSlot.Trade = orderBook;
                _ringBuffer.Publish(sequence);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
