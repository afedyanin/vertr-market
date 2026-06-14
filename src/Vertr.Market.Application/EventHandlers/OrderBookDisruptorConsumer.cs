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
        for (var i = 0; i < data.Count; i++)
        {
            ProcessSnapshot(in data[i], endOfBatch);
        }

        data.Clear();
    }

    private void ProcessSnapshot(in OrderBook book, bool endOfBatch)
    {
        var bids = book.Bids;
        var asks = book.Asks;

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