using Market.Core.Algos.Common;
using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

/// <summary>
/// Движок парного трейдинга на базе Фильтра Калмана с расчетом Z-Score
/// </summary>
public class KalmanMidPriceAlgo : PairTradingAlgoBase
{
    private readonly KalmanFilterEstimator _kalmanFilter;
    private readonly double _entryThreshold;
    private readonly double _exitThreshold;
    private readonly int _lotSizeX;
    private readonly int _lotSizeY;
    private readonly int _maxLots;

    public KalmanMidPriceAlgo(AlgoMetadata metadata) : base(metadata)
    {
        _lotSizeX = (int)AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.LotSizeX, 1.0);
        _lotSizeY = (int)AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.LotSizeY, 1.0);
        _maxLots = (int)AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.MaxLots, 10);
        _entryThreshold = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.EntryThreshold, 0.2);
        _exitThreshold = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.ExitThreshold, 0.01);

        var initialBeta = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.InitialBeta, 1.0);
        var initialErrorCovariance = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.InitialErrorCovariance, 1.0);
        var q = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.Q, 0.00005);
        var r = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.R, 0.01);

        _kalmanFilter = new KalmanFilterEstimator(
            initialBeta: initialBeta,
            initialErrorCovariance: initialErrorCovariance,
            q: q,
            r: r);
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

        // Цена приходит за лот. Нормализуем к цене за единицу актива
        var priceX = _lastDepthX.MidPrice!.Value / _lotSizeX;
        var priceY = _lastDepthY.MidPrice!.Value / _lotSizeY;
        _kalmanFilter.Update(priceX, priceY);

        (var xQty, var yQty) = GetQty();

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
        signal.Params[SignalParamKeys.Beta] = _kalmanFilter.Beta;
        signal.Params[SignalParamKeys.Spread] = _kalmanFilter.Spread;
        signal.Params[SignalParamKeys.ZScore] = _kalmanFilter.ZScore;
        signal.Params[SignalParamKeys.Sigma] = _kalmanFilter.Sigma;

        _openXQtyLots += xQty;
        _openYQtyLots += yQty;

        return signal;
    }

    private (int xQty, int yQty) GetQty()
    {
        var zScore = _kalmanFilter.ZScore;

        if (Math.Abs(zScore) < _exitThreshold && HasOpenPosition)
        {
            // Спред вернулся в норму, фиксируем прибыль
            return (-_openXQtyLots, -_openYQtyLots);
        }

        (var xQty, var yQty) = ApproximateLotCalculator.CalculateLots(
            _kalmanFilter.Beta,
            _lotSizeX,
            _lotSizeY,
            _maxLots);

        if (zScore > _entryThreshold && !HasOpenPosition)
        {
            return (xQty, -yQty);
        }
        else if (zScore < -_entryThreshold && !HasOpenPosition)
        {
            return (-xQty, yQty);
        }

        return (0, 0);
    }
}
