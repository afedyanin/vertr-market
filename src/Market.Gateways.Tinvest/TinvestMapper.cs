using System.Runtime.CompilerServices;
using Market.ApiClient.Dtos;
using Tinkoff.InvestApi.V1;

namespace Market.Gateways.Tinvest;

internal static class TinvestMapper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MarketDepthDto ToMarketDepth(OrderBook orderBook, ushort assetId)
    {
        var bidsBuffer = new PriceLevelDto[10];
        var asksBuffer = new PriceLevelDto[10];

        var bidsCount = Math.Min(orderBook.Bids.Count, 10);
        for (var i = 0; i < bidsCount; i++)
        {
            var protoBid = orderBook.Bids[i];

            bidsBuffer[i] = new PriceLevelDto(
                Price: protoBid.Price,
                Volume: (uint)protoBid.Quantity
            );
        }

        var asksCount = Math.Min(orderBook.Asks.Count, 10);
        for (var i = 0; i < asksCount; i++)
        {
            var protoAsk = orderBook.Asks[i];

            asksBuffer[i] = new PriceLevelDto(
                Price: protoAsk.Price,
                Volume: (uint)protoAsk.Quantity
            );
        }

        return new MarketDepthDto(assetId, orderBook.Time?.ToDateTime() ?? default, bidsBuffer, asksBuffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TradeTickDto ToTradeTick(Trade protoTrade, ushort assetId)
    {
        var side = (byte)(protoTrade.Direction == TradeDirection.Sell ? 1 : 0);

        return new TradeTickDto(
            Timestamp: protoTrade.Time.ToDateTime(),
            Price: protoTrade.Price,
            Volume: (uint)protoTrade.Quantity,
            AssetId: assetId,
            Side: side
        );
    }
}

