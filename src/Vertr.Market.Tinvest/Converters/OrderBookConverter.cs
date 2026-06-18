using System.Runtime.CompilerServices;
using Vertr.Market.Application.Models;
namespace Vertr.Market.Tinvest.Converters;

internal static class OrderBookConverter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OrderBook Convert(this Tinkoff.InvestApi.V1.OrderBook orderBook, int assetId)
    {
        return new OrderBook(
            AssetId: assetId,
            Timestamp: orderBook.Time.ToDateTime(),
            Bids: orderBook.Bids.Convert(),
            Asks: orderBook.Asks.Convert()
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LevelBuffer Convert(this Google.Protobuf.Collections.RepeatedField<Tinkoff.InvestApi.V1.Order> orders)
    {
        var lb = new LevelBuffer();
        var limit = Math.Min(orders.Count, Consts.OrderBookDepth);

        for (var i = 0; i < limit; i++)
        {
            lb[i] = orders[i].Convert();
        }

        return lb;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OrderBookLevel Convert(this Tinkoff.InvestApi.V1.Order order)
        => new OrderBookLevel(order.Price, order.Quantity);
}