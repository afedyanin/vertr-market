using Market.Core.Algos.Common;
using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

public class DynamicKalmanMidPriceAlgo : PairTradingAlgoBase
{
    // Пороговые значения для генерации сигналов
    private const double EntryThreshold = 2.0;  // Вход при Z-Score > 2.0 или < -2.0
    private const double ExitThreshold = 0.5;   // Выход (тейк-профит) при приближении к 0
    private readonly DynamicKalmanFilterEstimator _kalmanFilter;

    public DynamicKalmanMidPriceAlgo(
        ushort assetX,
        ushort assetY,
        double initialBeta = 1.0,
        double initialErrorCovariance = 1.0,
        double q = 0.0001,
        double r = 1.0) : base(assetX, assetY)
    {
        _kalmanFilter = new DynamicKalmanFilterEstimator(
            initialBeta: initialBeta,
            initialErrorCovariance: initialErrorCovariance,
            q: q,
            r: r);
    }

    protected override ITradingSignal? ProcessTickX()
    {
        _kalmanFilter.UpdateX(_lastDepthX!.MidPrice!.Value, _lastDepthX.Timestamp);
        return ProcessTick();
    }

    protected override ITradingSignal? ProcessTickY()
    {
        _kalmanFilter.UpdateY(_lastDepthY!.MidPrice!.Value, _lastDepthY.Timestamp);
        return ProcessTick();
    }

    private ITradingSignal? ProcessTick()
    {
        if (!_kalmanFilter.HasValidData)
        {
            return null;
        }

        var zScore = _kalmanFilter.ZScore;

        string signal = "HOLD";

        if (zScore > EntryThreshold)
        {
            signal = "SHORT Y / LONG X"; // Спред слишком большой, ставим на сужение
            _currentPosition = PositionState.ShortY_LongX;
        }
        else if (zScore < -EntryThreshold)
        {
            signal = "LONG Y / SHORT X"; // Спред слишком маленький, ставим на расширение
            _currentPosition = PositionState.LongY_ShortX;
        }
        else if (Math.Abs(zScore) < ExitThreshold && _currentPosition != PositionState.None)
        {
            signal = "EXIT POSITIONS";   // Спред вернулся в норму, фиксируем прибыль
            _currentPosition = PositionState.None;
        }

        return new KalmanPairsAlgoSignal
        {
            PriceX = _lastDepthX!.MidPrice!.Value,
            PriceY = _lastDepthY!.MidPrice!.Value,
            Beta = _kalmanFilter.Beta,
            Spread = _kalmanFilter.Spread,
            ZScore = _kalmanFilter.ZScore,
            Signal = signal,
            AssetX = AssetX,
            AssetY = AssetY,
        };
    }
}
