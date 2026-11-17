using Market.Core.Algos.Common;
using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

public class DynamicKalmanMidPriceAlgo : PairTradingAlgoBase
{
    private readonly double _entryThreshold;
    private readonly double _exitThreshold;
    private readonly decimal _requiredYQty;

    private readonly DynamicKalmanFilterEstimator _kalmanFilter;

    public DynamicKalmanMidPriceAlgo(AlgoMetadata metadata) : base(metadata)
    {
        _requiredYQty = (decimal)AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.RequiredYQty, 1.0);
        _entryThreshold = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.EntryThreshold, 0.2);
        _exitThreshold = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.ExitThreshold, 0.01);

        var initialBeta = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.InitialBeta, 1.0);
        var initialErrorCovariance = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.InitialErrorCovariance, 1.0);
        var q = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.Q, 0.00005);
        var r = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.R, 0.01);

        _kalmanFilter = new DynamicKalmanFilterEstimator(
            initialBeta: initialBeta,
            initialErrorCovariance: initialErrorCovariance,
            q: q,
            r: r);
    }

    protected override PairTradingSignal? ProcessTickX()
    {
        _kalmanFilter.UpdateX(_lastDepthX!.MidPrice!.Value, _lastDepthX.Timestamp);
        return ProcessTick();
    }

    protected override PairTradingSignal? ProcessTickY()
    {
        _kalmanFilter.UpdateY(_lastDepthY!.MidPrice!.Value, _lastDepthY.Timestamp);
        return ProcessTick();
    }

    private PairTradingSignal? ProcessTick()
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
            XQtyLots = xQty,
            YQtyLots = yQty,
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

        if (zScore > _entryThreshold && !HasOpenPosition)
        {
            yQty = -_requiredYQty;
            xQty = _requiredYQty / (decimal)_kalmanFilter.Beta;
        }
        else if (zScore < -_entryThreshold && !HasOpenPosition)
        {
            yQty = _requiredYQty;
            xQty = -_requiredYQty / (decimal)_kalmanFilter.Beta;
        }
        else if (Math.Abs(zScore) < _exitThreshold && HasOpenPosition)
        {
            // Спред вернулся в норму, фиксируем прибыль
            yQty = -_openYQtyLots;
            xQty = -_openXQtyLots;
        }

        return (xQty, yQty);
    }
}
