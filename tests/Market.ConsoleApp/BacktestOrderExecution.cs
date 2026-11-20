using Market.Core.Models;

namespace Market.BacktestEngine;

public class BacktestOrderExecution
{
    public IEnumerable<MyTrade> Execute(PairTradingSignal signal)
    {
        var trades = new List<MyTrade>();
        trades.AddRange(GenerateTrades(signal.StrategyId, signal.XQtyLots, signal.BookX));
        trades.AddRange(GenerateTrades(signal.StrategyId, signal.YQtyLots, signal.BookY));

        return trades;
    }

    private MyTrade[] GenerateTrades(Guid strategyId, decimal qty, OrderBookDepth orderBook)
    {
        if (qty == 0)
        {
            return [];
        }

        var bid = orderBook.BestBid;
        var ask = orderBook.BestAsk;

        if (bid == 0 || ask == 0) // неполный стакан
        {
            return [];
        }

        // TODO: Implement trades filling by Price Levels
        var price = qty > 0 ? ask : bid;

        if (!price.HasValue)
        {
            return [];
        }

        var trade = new MyTrade()
        {
            StrategyId = strategyId,
            Timestamp = orderBook.Timestamp,
            AssetId = orderBook.AssetId,
            Qty = qty,
            Price = (decimal)price.Value,
        };

        return [trade];
    }
}
