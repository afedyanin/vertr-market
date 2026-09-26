using System.Runtime.CompilerServices;
using Market.Core.Models;
using Tinkoff.InvestApi.V1;

namespace Market.Gateways.Tinvest;

internal static class TinvestMapper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OrderBookDepth ToMarketDepth(OrderBook orderBook, ushort assetId)
    {
        var bidsBuffer = new OrderBookLevel[10];
        var asksBuffer = new OrderBookLevel[10];

        var bidsCount = Math.Min(orderBook.Bids.Count, 10);
        for (var i = 0; i < bidsCount; i++)
        {
            var protoBid = orderBook.Bids[i];

            bidsBuffer[i] = new OrderBookLevel(
                price: (double)(decimal)protoBid.Price,
                size: protoBid.Quantity
            );
        }

        var asksCount = Math.Min(orderBook.Asks.Count, 10);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TradeTick ToTradeTick(Trade protoTrade, ushort assetId)
    {
        var side = (byte)(protoTrade.Direction == TradeDirection.Sell ? 1 : 0);

        return new TradeTick(
            Timestamp: protoTrade.Time.ToDateTime(),
            Price: protoTrade.Price,
            Volume: (uint)protoTrade.Quantity,
            AssetId: assetId,
            Side: side
        );
    }
}

