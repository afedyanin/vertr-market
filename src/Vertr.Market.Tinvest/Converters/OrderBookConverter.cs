using Vertr.Market.Application.Models;
namespace Vertr.Market.Tinvest.Converters;

internal static class OrderBookConverter
{
    public static OrderBook Convert(this Tinkoff.InvestApi.V1.OrderBook orderBook, int assetId)
        => new OrderBook
        {
            AssetId = assetId,
            Timestamp = orderBook.Time.ToDateTime(),
            Bids = orderBook.Bids.ToArray().Convert(),
            Asks = orderBook.Asks.ToArray().Convert(),
        };

    public static LevelBuffer Convert(this Tinkoff.InvestApi.V1.Order[] orders)
    {
        var lb = new LevelBuffer();
        for (var i = 0; i <= Consts.OrderBookDepth; i++)
        {
            lb[i] = orders[i].Convert();
        }

        return lb;
    }

    public static OrderBookLevel Convert(this Tinkoff.InvestApi.V1.Order order)
        => new OrderBookLevel
        {
            Price = order.Price,
            Volume = order.Quantity,
        };
}
