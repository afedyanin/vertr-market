using Vertr.Market.Application.Models;

namespace Vertr.Market.Tinvest.Converters;

internal static class TradeConverter
{
    public static Trade Convert(this Tinkoff.InvestApi.V1.Trade trade, int assetId)
        => new Trade
        {
            AssetId = assetId,
            Timestamp = trade.Time.ToDateTime(),
            Price = trade.Price,
            Volume = trade.Quantity,
        };
}
