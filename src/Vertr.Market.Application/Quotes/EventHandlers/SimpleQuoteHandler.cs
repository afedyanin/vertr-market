using Disruptor;

namespace Vertr.Market.Application.Quotes.EventHandlers;

internal sealed class SimpleQuoteHandler : IEventHandler<QuoteAggregatedEvent>
{
    public void OnEvent(QuoteAggregatedEvent data, long sequence, bool endOfBatch)
    {
    }
}
