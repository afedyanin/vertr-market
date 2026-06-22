using System.Runtime.CompilerServices;
using Vertr.Market.Application.Trades;

namespace Vertr.Market.Tinvest.Converters;

internal static class TradeConverter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Trade Convert(this Tinkoff.InvestApi.V1.Trade trade, int assetId)
    {
        return new Trade(
            AssetId: assetId,
            Price: trade.Price,
            Volume: trade.Quantity,
            Timestamp: trade.Time.ToDateTime()
        );
    }
}


