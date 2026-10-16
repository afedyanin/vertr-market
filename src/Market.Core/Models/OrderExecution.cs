namespace Market.Core.Models;

public class OrderExecution : IOrderExecutionEngine
{
    public IEnumerable<MyTrade> Execute(IPairTradingSignal signal)
    {
        var trades = new List<MyTrade>();
        trades.AddRange(GenerateTrades(signal.SourceId, signal.XQty, signal.BookX));
        trades.AddRange(GenerateTrades(signal.SourceId, signal.YQty, signal.BookY));

        return trades;
    }

    private MyTrade[] GenerateTrades(string sourceId, decimal qty, OrderBookDepth orderBook)
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
            SourceId = sourceId,
            Timestamp = orderBook.Timestamp,
            AssetId = orderBook.AssetId,
            Qty = qty,
            Price = (decimal)price.Value,
        };

        return [trade];
    }
}
