using Market.Core.Algos.Common;
using Market.Core.Models;

namespace Market.Core.Algos.PairTradingV2;

public class KalmanFilterBasicStrategy : PairTradingAlgoBase
{
    private readonly PairsKalmanFilter _kalmanFilter;

    private readonly double _entryThreshold;
    private readonly double _exitThreshold;
    private readonly int _lotSizeX;
    private readonly int _lotSizeY;
    private readonly int _maxLots;
    private readonly double _priceRatio;

    public KalmanFilterBasicStrategy(AlgoMetadata metadata) : base(metadata)
    {
        _kalmanFilter = new PairsKalmanFilter(delta: 1e-4, obsNoise: 1.0);

        _lotSizeX = (int)AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.LotSizeX, 1.0);
        _lotSizeY = (int)AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.LotSizeY, 1.0);
        _priceRatio = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.PriceRatio, 1.0);
        _maxLots = (int)AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.MaxLots, 10);
        _entryThreshold = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.EntryThreshold, 2.0);
        _exitThreshold = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.ExitThreshold, 0.01);
    }

    protected override PairTradingSignal? ProcessTickX()
    {
        if (_lastDepthX is null)
        {
            return null;
        }

        return ProcessTick();
    }

    protected override PairTradingSignal? ProcessTickY()
    {
        if (_lastDepthY is null)
        {
            return null;
        }

        return ProcessTick();
    }

    private PairTradingSignal? ProcessTick()
    {
        // Пока не получили данные по обоим инструментам — расчет невозможен
        if (_lastDepthX is null || _lastDepthY is null)
        {
            return null;
        }

        var priceX = _lastDepthX.MidPrice!.Value * _priceRatio;
        var priceY = _lastDepthY.MidPrice!.Value;
        var stepResult = _kalmanFilter.Update(priceX, priceY);

        (var xQty, var yQty) = GetQty(stepResult);

        if (xQty == 0 && yQty == 0)
        {
            return null;
        }

        var signal = new PairTradingSignal
        {
            //SourceId = $"{nameof(KalmanMidPriceAlgo)}:{AssetX}:{AssetY}",
            StrategyId = StrategyId,
            BookX = _lastDepthX,
            BookY = _lastDepthY,
            XQtyLots = xQty,
            YQtyLots = yQty,
        };

        signal.Params[SignalParamKeys.PriceX] = priceX;
        signal.Params[SignalParamKeys.PriceY] = priceY;
        signal.Params[SignalParamKeys.Alpha] = stepResult.Alpha;
        signal.Params[SignalParamKeys.Beta] = stepResult.Beta;
        signal.Params[SignalParamKeys.Spread] = stepResult.Spread;
        signal.Params[SignalParamKeys.ZScore] = stepResult.ZScore;

        _openXQtyLots += xQty;
        _openYQtyLots += yQty;

        return signal;
    }

    private (int xQty, int yQty) GetQty(PairsKalmanFilter.StepResult stepResult)
    {
        var zScore = stepResult.ZScore;

        if (Math.Abs(zScore) < _exitThreshold && HasOpenPosition)
        {
            // Спред вернулся в норму, фиксируем прибыль
            return (-_openXQtyLots, -_openYQtyLots);
        }

        (var xQty, var yQty) = ApproximateLotCalculator.CalculateLots(
            stepResult.Beta,
            _lotSizeX,
            _lotSizeY,
            _maxLots);

        if (zScore > _entryThreshold && !HasOpenPosition)
        {
            // Short Y, Long X
            return (xQty, -yQty);
        }
        else if (zScore < -_entryThreshold && !HasOpenPosition)
        {
            // Long Y, Short X
            return (-xQty, yQty);
        }

        return (0, 0);
    }
}
