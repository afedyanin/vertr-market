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

        var zScore = _kalmanFilter.ZScore;

        var action = PairTradingAction.Hold;

        if (zScore > EntryThreshold)
        {
            action = PairTradingAction.ShortY_LongX;
            _currentState = StrategyState.ShortY_LongX;
        }
        else if (zScore < -EntryThreshold)
        {
            action = PairTradingAction.LongY_ShortX; // Спред слишком маленький, ставим на расширение
            _currentState = StrategyState.LongY_ShortX;
        }
        else if (Math.Abs(zScore) < ExitThreshold && _currentState != StrategyState.None)
        {
            action = PairTradingAction.Exit; // Спред вернулся в норму, фиксируем прибыль
            _currentState = StrategyState.None;
        }

        var signal = new PairTradingSignal
        {
            SourceId = $"{nameof(DynamicKalmanMidPriceAlgo)}:{AssetX}:{AssetY}",
            Action = action,
            BookX = _lastDepthX!,
            BookY = _lastDepthY!,
        };

        signal.Params[SignalParamKeys.Beta] = _kalmanFilter.Beta;
        signal.Params[SignalParamKeys.Spread] = _kalmanFilter.Spread;
        signal.Params[SignalParamKeys.ZScore] = _kalmanFilter.ZScore;

        return signal;
    }
}
