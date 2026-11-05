using Market.Core.Algos.PairTrading;
using Market.Core.Models;

namespace Market.Core.Algos;

public static class AlgoFactory
{
    public static IPairTradingAlgo Create(AlgoMetadata algoMetadata)
    {
        return algoMetadata.AlgoType switch
        {
            nameof(KalmanMidPriceAlgo) => new KalmanMidPriceAlgo(algoMetadata),
            nameof(KalmanHalfLifeMidPriceAlgo) => new KalmanHalfLifeMidPriceAlgo(algoMetadata),
            nameof(DynamicKalmanMidPriceAlgo) => new DynamicKalmanMidPriceAlgo(algoMetadata),
            nameof(DynamicKalmanVwapAlgo) => new DynamicKalmanVwapAlgo(algoMetadata),
            _ => throw new NotImplementedException($"Unknown AlgoType={algoMetadata.AlgoType}"),
        };
    }
}
