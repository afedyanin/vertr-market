using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.Abstractions;

public class TradeProcessor
{
    private readonly GenericMarketDataManager<Bar, MarketDataEvent> _dataManager;

    public TradeProcessor(GenericMarketDataManager<Bar, MarketDataEvent> dataManager)
    {
        _dataManager = dataManager;
    }

    /// <summary>
    /// ВЫЗЫВАЕТСЯ ИЗ ЛЮБОГО ПОТОКА ПРИ ИГРЕ СДЕЛКИ.
    /// Парсит сделку и отправляет её в нужный индекс массива.
    /// </summary>
    public void HandleIncomingTrade(Trade trade)
    {
        var index = trade.InstrumentId;

        // Приведение типов: decimal в double для высокой скорости расчетов в CPU
        var price = (double)trade.Price;
        var quantity = trade.Quantity;

        // Извлекаем конкретную ячейку хранения по индексу инструмента
        AtomicCell<Bar> cell = _dataManager.GetCell(index);

        // ВАЖНО: Нам нужно обновить данные внутри активного буфера записи ячейки.
        // Чтобы не городить lock, мы делегируем логику агрегации внутрь метода ячейки.
        cell.UpdateInPlace(writeBuffer =>
        {
            writeBuffer.AggregateTrade(price, quantity);
        });
    }
}