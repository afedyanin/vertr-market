using Market.Core.Models;
using Tinkoff.InvestApi.V1;

namespace Market.Gateways.Tinvest;

internal static class TinvestMapper
{
    public static OrderBookDepth ToMarketDepth(OrderBook orderBook, ushort assetId)
    {
        var bidsCount = Math.Min(orderBook.Bids.Count, 10);
        var asksCount = Math.Min(orderBook.Asks.Count, 10);

        var bidsBuffer = new OrderBookLevel[bidsCount];
        var asksBuffer = new OrderBookLevel[asksCount];

        for (var i = 0; i < bidsCount; i++)
        {
            var protoBid = orderBook.Bids[i];

            bidsBuffer[i] = new OrderBookLevel(
                price: (double)(decimal)protoBid.Price,
                size: protoBid.Quantity
            );
        }

        for (var i = 0; i < asksCount; i++)
        {
            var protoAsk = orderBook.Asks[i];

            asksBuffer[i] = new OrderBookLevel(
                price: (double)(decimal)protoAsk.Price,
                size: protoAsk.Quantity
            );
        }

        return new OrderBookDepth(
            orderBook.Time.ToDateTime(),
            assetId,
            bidsBuffer,
            asksBuffer);
    }

    public static IEnumerable<MyTrade> ToMyTrades(OrderTrades orderTrades, ushort assetId, Guid? strategyId)
    {
        var direction = orderTrades.Direction == OrderDirection.Sell ? -1 : orderTrades.Direction == OrderDirection.Buy ? 1 : 0;

        foreach (var trade in orderTrades.Trades)
        {
            yield return new MyTrade
            {
                AssetId = assetId,
                StrategyId = strategyId,
                TradeId = trade.TradeId,
                OrderId = orderTrades.OrderId,
                Price = trade.Price,
                Qty = trade.Quantity * direction,
                Timestamp = trade.DateTime.ToDateTime(),
            };
        }
    }

    public static Core.Models.Asset ToAsset(Instrument instrument)
        => new Core.Models.Asset
        {
            ExternalId = instrument.Uid,
            Isin = instrument.Isin,
            Ticker = instrument.Ticker,
            ClassCode = instrument.ClassCode,
            Name = instrument.Name,
            Currency = instrument.Currency,
            LotSize = instrument.Lot,
            MinPriceIncrement = instrument.MinPriceIncrement,
            AssetType = instrument.InstrumentType,
            AssetKind = instrument.InstrumentKind.ToString(),
        };

    public static Core.Models.Asset ToAsset(Share share)
        => new Core.Models.Asset
        {
            ExternalId = share.Uid,
            Isin = share.Isin,
            Ticker = share.Ticker,
            ClassCode = share.ClassCode,
            Name = share.Name,
            Currency = share.Currency,
            LotSize = share.Lot,
            MinPriceIncrement = share.MinPriceIncrement,
            AssetType = share.ShareType.ToString(),
        };

    public static Core.Models.Asset ToAsset(Future future)
        => new Core.Models.Asset
        {
            ExternalId = future.Uid,
            Ticker = future.Ticker,
            ClassCode = future.ClassCode,
            Name = future.Name,
            Currency = future.Currency,
            ExpDate = future.ExpirationDate.ToDateTime(),
            LotSize = future.Lot,
            MinPriceIncrement = future.MinPriceIncrement,
            MinPriceIncrementAmount = future.MinPriceIncrementAmount,
            BasicAssetSize = future.BasicAssetSize,
            BasicAsset = future.BasicAsset,
            AssetType = future.AssetType,
            AssetKind = future.FuturesType,
        };
}

