using System.Buffers;
using Disruptor;

namespace Vertr.Market.Application.Quotes.EventHandlers;

public sealed class MemoryReleaseHandler : IEventHandler<QuoteAggregatedEvent>
{
    public void OnEvent(QuoteAggregatedEvent data, long sequence, bool endOfBatch)
    {
        if (data.Quotes != null)
        {
            ArrayPool<Quote>.Shared.Return(data.Quotes, clearArray: false);
            data.Quotes = null;
        }
    }
}