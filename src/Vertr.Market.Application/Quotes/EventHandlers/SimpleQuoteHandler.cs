using Disruptor;

namespace Vertr.Market.Application.Quotes.EventHandlers;

internal sealed class SimpleQuoteHandler : IEventHandler<QuoteAggregatedEvent>
{
    public void OnEvent(QuoteAggregatedEvent data, long sequence, bool endOfBatch)
    {
        // Правильный способ чтения данных:
        for (var i = 0; i < data.Count; i++)
        {
#pragma warning disable IDE0059 // Unnecessary assignment of a value
            var currentQuote = data.Quotes![i];
#pragma warning restore IDE0059 // Unnecessary assignment of a value
        }
    }
}
