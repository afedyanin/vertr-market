using Market.Core.Algos.Common;
using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

/// <summary>
/// Движок парного трейдинга на базе Фильтра Калмана с расчетом Z-Score
/// </summary>
public class KalmanMidPriceAlgo : PairTradingAlgoBase
{
    // Пороговые значения для генерации сигналов
    private const double EntryThreshold = 2.0;  // Вход при Z-Score > 2.0 или < -2.0
    private const double ExitThreshold = 0.5;   // Выход (тейк-профит) при приближении к 0
    private readonly KalmanFilterEstimator _kalmanFilter;

    public KalmanMidPriceAlgo(
        ushort assetX,
        ushort assetY,
        double initialBeta = 1.0,
        double initialErrorCovariance = 1.0,
        double q = 0.0001,
        double r = 1.0) : base(assetX, assetY)
    {
        _kalmanFilter = new KalmanFilterEstimator(
            initialBeta: initialBeta,
            initialErrorCovariance: initialErrorCovariance,
            q: q,
            r: r);
    }

    protected override ITradingSignal? ProcessTickX()
    {
        if (_lastDepthX is null)
        {
            return null;
        }

        return ProcessTick();
    }

    protected override ITradingSignal? ProcessTickY()
    {
        if (_lastDepthY is null)
        {
            return null;
        }

        return ProcessTick();
    }

    private ITradingSignal? ProcessTick()
    {
        // Пока не получили данные по обоим инструментам — расчет невозможен
        if (_lastDepthX is null || _lastDepthY is null)
        {
            return null;
        }

        var priceX = _lastDepthX.MidPrice!.Value;
        var priceY = _lastDepthY.MidPrice!.Value;
        _kalmanFilter.Update(priceX, priceY);

        var zScore = _kalmanFilter.ZScore;

        var action = PairTradingAction.Hold;

        if (zScore > EntryThreshold)
        {
            action = PairTradingAction.ShortY_LongX; // Спред слишком большой, ставим на сужение
            _currentState = StrategyState.ShortY_LongX;
        }
        else if (zScore < -EntryThreshold)
        {
            action = PairTradingAction.LongY_ShortX; // Спред слишком маленький, ставим на расширение
            _currentState = StrategyState.LongY_ShortX;
        }
        else if (Math.Abs(zScore) < ExitThreshold && _currentState != StrategyState.None)
        {
            action = PairTradingAction.Exit;   // Спред вернулся в норму, фиксируем прибыль
            _currentState = StrategyState.None;
        }

        return new KalmanPairsAlgoSignal
        {
            SourceId = $"{nameof(KalmanMidPriceAlgo)}:{AssetX}:{AssetY}",
            PriceX = priceX,
            PriceY = priceY,
            Beta = _kalmanFilter.Beta,
            Spread = _kalmanFilter.Spread,
            ZScore = _kalmanFilter.ZScore,
            Action = action,
            BookX = _lastDepthX,
            BookY = _lastDepthY,
        };
    }
}

public record KalmanPairsAlgoSignal : ITradingSignal
{
    public required string SourceId { get; set; }
    public required OrderBookDepth BookX { get; set; }
    public required OrderBookDepth BookY { get; set; }

    public double Beta { get; set; }
    public double ZScore { get; set; }
    public double Spread { get; set; }
    public PairTradingAction Action { get; set; }
    public double PriceX { get; set; }
    public double PriceY { get; set; }
}
