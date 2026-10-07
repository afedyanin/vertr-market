using System.Runtime.CompilerServices;
using Market.Core.Models;
using Tinkoff.InvestApi.V1;

namespace Market.Gateways.Tinvest;

internal static class TinvestMapper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
}

