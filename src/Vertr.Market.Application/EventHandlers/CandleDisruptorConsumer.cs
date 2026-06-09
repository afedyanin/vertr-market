using Disruptor;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.EventHandlers;

public sealed class CandleDisruptorConsumer : IEventHandler<CandleEvent>
{
    /// <summary>
    /// Метод выполняется автоматически в выделенном потоке Disruptor при появлении новых свечей.
    /// </summary>
    public void OnEvent(CandleEvent data, long sequence, bool endOfBatch)
    {
        // Передаем структуру по ссылке (in), минимизируя накладные расходы на копирование в стек
        ProcessCandle(in data.Value, endOfBatch);
    }

    private void ProcessCandle(in Candle candle, bool endOfBatch)
    {
        // Бизнес-логика: отправка свечи в торговую стратегию, отрисовка на графике, запись в БД.
        // Использование флага endOfBatch позволяет делать пакетную запись (Batching) нескольких свечей за раз.

        // Переиспользование памяти происходит автоматически на стороне Disruptor.

        Console.WriteLine($"Candle: {candle}");
    }
}
