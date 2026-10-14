namespace Market.Core.Models;

public class OrderExecution : IOrderExecutionEngine
{
    public IEnumerable<MyTrade> Execute(IPairTradingSignal signal, Portfolio portfolio)
    {
        var trades = new List<MyTrade>();

        var positionX = portfolio.Get(signal.BookX.AssetId);
        var positionY = portfolio.Get(signal.BookY.AssetId);

        var positionXState = positionX?.State ?? PositionState.None;
        var positionYState = positionY?.State ?? PositionState.None;

        switch (signal.Action)
        {
            // TODO: Check lot size
            case PairTradingAction.Exit:
                if (positionXState != PositionState.None)
                {
                    trades.AddRange(GenerateTrades(signal.SourceId, -positionX!.Quantity, signal.BookX));
                }

                if (positionYState != PositionState.None)
                {
                    trades.AddRange(GenerateTrades(signal.SourceId, -positionY!.Quantity, signal.BookY));
                }

                break;
            case PairTradingAction.LongY_ShortX:
                if (positionYState == PositionState.Long || positionXState == PositionState.Short)
                {
                    // already open
                    break;
                }

                trades.AddRange(GenerateTrades(signal.SourceId, signal.YQty, signal.BookY));
                trades.AddRange(GenerateTrades(signal.SourceId, -signal.XQty, signal.BookX));
                break;

            case PairTradingAction.ShortY_LongX:
                if (positionYState == PositionState.Short || positionXState == PositionState.Long)
                {
                    // already open
                    break;
                }

                trades.AddRange(GenerateTrades(signal.SourceId, -signal.YQty, signal.BookY));
                trades.AddRange(GenerateTrades(signal.SourceId, signal.XQty, signal.BookX));
                break;

            default:
                break;
        }

        return trades;
    }

    private MyTrade[] GenerateTrades(string sourceId, decimal qty, OrderBookDepth orderBook)
    {
        if (qty == 0)
        {
            return [];
        }

        // TODO: Implement trades filling by Price Levels
        var price = qty > 0 ? orderBook.BestAsk : orderBook.BestBid;

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
