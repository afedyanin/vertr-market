namespace Vertr.Market.Application.Models;

public class TradeProcessor
{
    private readonly GenericMarketDataManager<Bar, MarketDataEvent> _dataManager;

    public TradeProcessor(GenericMarketDataManager<Bar, MarketDataEvent> dataManager)
    {
        _dataManager = dataManager;
    }

    public void HandleIncomingTrade(Trade trade)
    {
        var index = trade.InstrumentId;
        var price = (double)trade.Price;
        var quantity = trade.Quantity;

        var cell = _dataManager.GetCell(index);

        cell.UpdateInPlace(writeBuffer =>
        {
            writeBuffer.AggregateTrade(price, quantity);
        });
    }
}