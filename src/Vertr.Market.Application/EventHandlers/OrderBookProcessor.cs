using System.Runtime.CompilerServices;
using Disruptor;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.EventHandlers;

public sealed class OrderBookProcessor : IEventHandler<OrderBookEvent>
{
    private readonly int _processorId;
    private readonly int _totalProcessors;

    // Внутреннее состояние (стейт) обработчика для конкретных активов.
    // Так как поток у обработчика всегда один и тот же, этот словарь не требует локов!
    private readonly Dictionary<int, long> _lastTimestamps = new();

    public OrderBookProcessor(int processorId, int totalProcessors)
    {
        if (processorId < 0 || processorId >= totalProcessors)
        {
            throw new ArgumentOutOfRangeException(nameof(processorId), "ID должен быть от 0 до total-1");
        }

        _processorId = processorId;
        _totalProcessors = totalProcessors;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnEvent(OrderBookEvent data, long sequence, bool endOfBatch)
    {
        // Шардирование по AssetId. 
        // Гарантирует, что один и тот же актив ВСЕГДА обрабатывается только ОДНИМ конкретным потоком.
        // При этом разные активы обрабатываются параллельно на разных ядрах CPU.
        if ((data.OrderBook.AssetId % _totalProcessors) != _processorId)
        {
            return;
        }

        // Передаем структуру по ссылке, чтобы избежать копирования тяжелого OrderBook на стек
        ProcessOrderBook(in data.OrderBook, endOfBatch);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessOrderBook(in OrderBook book, bool endOfBatch)
    {
        // 1. Быстрая валидация последовательности (Защита от старых данных/Race на источнике)
        if (_lastTimestamps.TryGetValue(book.AssetId, out var lastTs) && book.Timestamp <= lastTs)
        {
            // Пропускаем устаревший стакан (Out-of-order пакет из сети)
            return;
        }

        _lastTimestamps[book.AssetId] = book.Timestamp;

        // 2. Бизнес-логика (например, матчинг ордеров, арбитраж или сохранение)
        // Для примера выведем спред лучшего бида/аска
        if (book.BidCount > 0 && book.AskCount > 0)
        {
            // InlineArray позволяет работать с элементами через безопасный ref без аллокаций
            ref readonly var bestBid = ref book.Bids[0];
            ref readonly var bestAsk = ref book.Asks[0];

#pragma warning disable IDE0059 // Unnecessary assignment of a value
            var spread = bestAsk.Price - bestBid.Price;
#pragma warning restore IDE0059 // Unnecessary assignment of a value

            // В реальной системе здесь будет вызов движка матчинга:
            // _matchingEngine.UpdateOrderBook(book.AssetId, in book);
        }

        // 3. Техника Batching (Пакетирование)
        if (endOfBatch)
        {
            // Конец пачки! Издатель пока не записал новых данных, поток освободился.
            // Идеальное место, чтобы сбросить накопленные метрики, сделать Flush в БД/лог 
            // или отправить агрегированное уведомление, не тормозя обработку каждого стакана.
            ExecuteBatchFlush();
        }
    }

    private void ExecuteBatchFlush()
    {
        // Логика тяжелого коммита/флаша, которая вызывается редко
    }
}
