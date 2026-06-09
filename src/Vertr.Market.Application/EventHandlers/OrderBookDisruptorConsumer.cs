using Disruptor;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.EventHandlers;

public sealed class OrderBookDisruptorConsumer : IEventHandler<OrderBookEvent>
{
    /// <summary>
    /// Метод вызывается автоматически в выделенном потоке Disruptor.
    /// </summary>
    /// <param name="data">Ссылка на пре-аллоцированный объект в Ring Buffer</param>
    /// <param name="sequence">Порядковый номер события</param>
    /// <param name="endOfBatch">Флаг конца пачки (true, если это последнее доступное событие на данный момент)</param>
    public void OnEvent(OrderBookEvent data, long sequence, bool endOfBatch)
    {
        // Передаем структуру по ссылке (in), чтобы избежать копирования 340+ байт в стек метода
        ProcessSnapshot(in data.Value, endOfBatch);

        // Никаких вызовов Release() или возвратов в пул делать НЕ НУЖНО. 
        // Disruptor зациклит эту память автоматически, когда поток обработки пойдет на следующий круг.
    }

    private void ProcessSnapshot(in OrderBook book, bool endOfBatch)
    {
        // Чтение инлайн-массива через ReadOnlySpan (0 аллокаций)
        ReadOnlySpan<OrderBookLevel> bids = book.Bids;
        ReadOnlySpan<OrderBookLevel> asks = book.Asks;

        if (book.BidCount > 0 && book.AskCount > 0)
        {
            // Пример бизнес-логики: получаем лучшие цены (Спред)
            var bestBid = bids[0].Price;
            var bestAsk = asks[0].Price;

            // Здесь ваша логика: отправка по WebSocket клиентам, запись в БД и т.д.
            // Использование флага endOfBatch позволяет делать пакетную запись (Batching) для оптимизации IO.

            Console.WriteLine($"BestBid={bestBid} BestAsk={bestAsk}");
        }
    }
}