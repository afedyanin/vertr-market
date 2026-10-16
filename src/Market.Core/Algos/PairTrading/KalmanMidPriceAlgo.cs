using Market.Core.Algos.Common;
using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

/// <summary>
/// Движок парного трейдинга на базе Фильтра Калмана с расчетом Z-Score
/// </summary>
public class KalmanMidPriceAlgo : PairTradingAlgoBase
{
    // Пороговые значения для генерации сигналов
    private const double EntryThreshold = 0.1;  // Вход при Z-Score > 2.0 или < -2.0
    private const double ExitThreshold = 0.01;   // Выход (тейк-профит) при приближении к 0
    private readonly KalmanFilterEstimator _kalmanFilter;

    private readonly decimal _requiredYQty = 1.0m;

    // размер лота в единицах актива
    private readonly int _lotSizeX;
    private readonly int _lotSizeY;

    public KalmanMidPriceAlgo(
        ushort assetX,
        ushort assetY,
        int lotSizeX,
        int lotSizeY,
        double initialBeta = 1.0,
        double initialErrorCovariance = 1.0,
        double q = 0.0001,
        double r = 1.0) : base(assetX, assetY)
    {
        _lotSizeX = lotSizeX;
        _lotSizeY = lotSizeY;

        _kalmanFilter = new KalmanFilterEstimator(
            initialBeta: initialBeta,
            initialErrorCovariance: initialErrorCovariance,
            q: q,
            r: r);
    }

    protected override IPairTradingSignal? ProcessTickX()
    {
        if (_lastDepthX is null)
        {
            return null;
        }

        return ProcessTick();
    }

    protected override IPairTradingSignal? ProcessTickY()
    {
        if (_lastDepthY is null)
        {
            return null;
        }

        return ProcessTick();
    }

    private IPairTradingSignal? ProcessTick()
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
            SourceId = $"{nameof(KalmanMidPriceAlgo)}:{AssetX}:{AssetY}",
            BookX = _lastDepthX,
            BookY = _lastDepthY,
            XQty = xQty,
            YQty = yQty,
        };

        signal.Params[SignalParamKeys.PriceX] = priceX;
        signal.Params[SignalParamKeys.PriceY] = priceY;
        signal.Params[SignalParamKeys.Beta] = _kalmanFilter.Beta;
        signal.Params[SignalParamKeys.Spread] = _kalmanFilter.Spread;
        signal.Params[SignalParamKeys.ZScore] = _kalmanFilter.ZScore;
        signal.Params[SignalParamKeys.Sigma] = _kalmanFilter.Sigma;

        _openXQty += xQty;
        _openYQty += yQty;

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
