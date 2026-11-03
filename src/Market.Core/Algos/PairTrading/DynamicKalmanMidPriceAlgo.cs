using Market.Core.Algos.Common;
using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

public class DynamicKalmanMidPriceAlgo : PairTradingAlgoBase
{
    // Пороговые значения для генерации сигналов
    private const double EntryThreshold = 2.0;  // Вход при Z-Score > 2.0 или < -2.0
    private const double ExitThreshold = 0.5;   // Выход (тейк-профит) при приближении к 0
    private readonly decimal _requiredYQty = 1.0m;

    private readonly DynamicKalmanFilterEstimator _kalmanFilter;

    public DynamicKalmanMidPriceAlgo(
        int strategyId,
        ushort assetX,
        ushort assetY,
        double initialBeta = 1.0,
        double initialErrorCovariance = 1.0,
        double q = 0.0001,
        double r = 1.0) : base(strategyId, assetX, assetY)
    {
        _kalmanFilter = new DynamicKalmanFilterEstimator(
            initialBeta: initialBeta,
            initialErrorCovariance: initialErrorCovariance,
            q: q,
            r: r);
    }

    protected override IPairTradingSignal? ProcessTickX()
    {
        _kalmanFilter.UpdateX(_lastDepthX!.MidPrice!.Value, _lastDepthX.Timestamp);
        return ProcessTick();
    }

    protected override IPairTradingSignal? ProcessTickY()
    {
        _kalmanFilter.UpdateY(_lastDepthY!.MidPrice!.Value, _lastDepthY.Timestamp);
        return ProcessTick();
    }

    private IPairTradingSignal? ProcessTick()
    {
        if (!_kalmanFilter.HasValidData)
        {
            return null;
        }

        (var xQty, var yQty) = GetQty();

        if (xQty == 0 && yQty == 0)
        {
            return null;
        }

        var signal = new PairTradingSignal
        {
            //SourceId = $"{nameof(DynamicKalmanMidPriceAlgo)}:{AssetX}:{AssetY}",
            StrategyId = StrategyId,
            BookX = _lastDepthX!,
            BookY = _lastDepthY!,
            XQty = xQty,
            YQty = yQty,
        };

        signal.Params[SignalParamKeys.Beta] = _kalmanFilter.Beta;
        signal.Params[SignalParamKeys.Spread] = _kalmanFilter.Spread;
        signal.Params[SignalParamKeys.ZScore] = _kalmanFilter.ZScore;
        signal.Params[SignalParamKeys.Sigma] = _kalmanFilter.Sigma;

        return signal;
    }
    private (decimal xQty, decimal yQty) GetQty()
    {
        var zScore = _kalmanFilter.ZScore;
        var xQty = 0m;
        var yQty = 0m;

        if (zScore > EntryThreshold && !HasOpenPosition)
        {
            yQty = -_requiredYQty;
            xQty = _requiredYQty / (decimal)_kalmanFilter.Beta;
        }
        else if (zScore < -EntryThreshold && !HasOpenPosition)
        {
            yQty = _requiredYQty;
            xQty = -_requiredYQty / (decimal)_kalmanFilter.Beta;
        }
        else if (Math.Abs(zScore) < ExitThreshold && HasOpenPosition)
        {
            // Спред вернулся в норму, фиксируем прибыль
            yQty = -_openYQty;
            xQty = -_openXQty;
        }

        return (xQty, yQty);
    }
}
