using Disruptor;
using Microsoft.Extensions.ObjectPool;

namespace Vertr.Market.Application.Quotes.EventHandlers;

public sealed class MemoryReleaseHandler : IEventHandler<QuoteAggregatedEvent>
{
    private readonly ObjectPool<Dictionary<int, Quote>> _dictionaryPool;

    public MemoryReleaseHandler(ObjectPool<Dictionary<int, Quote>> dictionaryPool)
    {
        _dictionaryPool = dictionaryPool;
    }

    public void OnEvent(QuoteAggregatedEvent data, long sequence, bool endOfBatch)
    {
        if (data.Quotes != null)
        {
            // Возвращаем в пул. Метод Return автоматически вызовет .Clear() из DictionaryPoolPolicy
            _dictionaryPool.Return(data.Quotes);

            // Зануляем ссылку в слоте RingBuffer
            data.Quotes = null;
        }
    }
}