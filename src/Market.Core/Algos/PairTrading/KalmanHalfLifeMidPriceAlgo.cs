using Market.Core.Algos.Common;
using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

public class KalmanHalfLifeMidPriceAlgo : PairTradingAlgoBase
{
    // Калман 2: Онлайн-оценка AR(1) для спреда (Модель Орнштейна-Уленбека)
    private int _secondsInTrade = 0;

    private readonly KalmanFilterEstimator _kalmanFilter;
    private readonly CointegrationEstimator _cointegration;

    public KalmanHalfLifeMidPriceAlgo(
        ushort assetX,
        ushort assetY,
        double initialBeta = 1.0) : base(assetX, assetY)
    {
        _kalmanFilter = new KalmanFilterEstimator(
            initialBeta: initialBeta,
            initialErrorCovariance: 1.0,
            q: 0.00005,
            r: 0.01);

        _cointegration = new CointegrationEstimator();
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
        if (_lastDepthX == null || _lastDepthY == null)
        {
            return null;
        }

        var priceX = _lastDepthX.MidPrice!.Value;
        var priceY = _lastDepthY.MidPrice!.Value;

        _kalmanFilter.Update(priceX, priceY);
        var beta = _kalmanFilter.Beta;
        var spread = _kalmanFilter.Spread;

        _cointegration.Update(_kalmanFilter.Spread);
        var isCointegrated = _cointegration.IsCointegrated;
        var halfLife = _cointegration.HalfLife;

        // РАСЧЕТ РЕАЛЬНЫХ ТОРГОВЫХ СПРЕДОВ С УЧЕТОМ BID/ASK
        // Спред, если мы заходим в LONG Y и SHORT X (Покупаем Y по Ask, продаем X по Bid)
        var spreadBuyY_SellX = _lastDepthY.BestAsk - (beta * _lastDepthX.BestBid);

        // Спред, если мы заходим в SHORT Y и LONG X (Продаем Y по Bid, покупаем X по Ask)
        var spreadSellY_BuyX = _lastDepthY.BestBid - (beta * _lastDepthX.BestAsk);

        // Переводим в Z-Score, используя общую сигму фильтра Калмана
        var sigma = _kalmanFilter.Sigma;
        var zScoreBuy = spreadBuyY_SellX / sigma;
        var zScoreSell = spreadSellY_BuyX / sigma;

        // 3. Логика сигналов с контролем полураспада
        string signal = "HOLD";

        if (_currentState == StrategyState.None && isCointegrated && halfLife > 1.0 && halfLife < 60.0)
        {
            // Вход в Short Y / Long X имеет смысл, только если спред ПРОДАЖИ (Sell) превысил верхний порог.
            // Мы будем бить по стакану: продавать Y по Bid и покупать X по Ask.
            if (zScoreSell > 2.0)
            {
                signal = $"EXECUTE: SHORT Y / LONG X";
                _currentState = StrategyState.ShortY_LongX;
                _secondsInTrade = 0;
            }
            // Вход в Long Y / Short X: покупаем Y по Ask, продаем X по Bid.
            else if (zScoreBuy < -2.0)
            {
                signal = $"EXECUTE: LONG Y / SHORT X";
                _currentState = StrategyState.LongY_ShortX;
                _secondsInTrade = 0;
            }
        }
        else if (_currentState == StrategyState.ShortY_LongX)
        {
            _secondsInTrade++;

            // СИГНАЛ ВЫХОДА ПО ПОЛУРАСПАДУ (Time Stop): 
            // Если позиция висит дольше, чем 2-3 периода полураспада, математическое ожидание прибыли исчезает.
            // Либо если коинтеграция полностью исчезла (isCointegrated == false)
            // Выход из позиции Short Y / Long X означает, что нам нужно КУПИТЬ Y (по Ask) и ПРОДАТЬ X (по Bid).
            // Поэтому мы отслеживаем zScoreBuy для выхода.
            if (zScoreBuy <= 0.2 || !isCointegrated || _secondsInTrade > (halfLife * 2.5))
            {
                signal = $"EXIT: BUY Y / SELL X (TAKE PROFIT)";
                _currentState = StrategyState.None;
                _secondsInTrade = 0;
            }
        }
        else if (_currentState == StrategyState.LongY_ShortX)
        {
            _secondsInTrade++;

            // Выход из позиции Long Y / Short X: ПРОДАЕМ Y (по Bid) и ПОКУПАЕМ X (по Ask).
            if (zScoreSell >= -0.2 || !isCointegrated || _secondsInTrade > (halfLife * 2.5))
            {
                signal = $"EXIT: SELL Y / BUY X (TAKE PROFIT)";
                _currentState = StrategyState.None;
                _secondsInTrade = 0;
            }
        }

        return new KalmanHalfLifePairsSignal
        {
            Beta = beta,
            Spread = spread,
            ZScoreBuy = zScoreBuy,
            ZScoreSell = zScoreSell,
            Signal = signal,
            HalfLifeSeconds = halfLife,
            IsCointegrated = isCointegrated,
            BookX = _lastDepthX,
            BookY = _lastDepthY,
        };
    }
}

public record KalmanHalfLifePairsSignal : ITradingSignal
{
    public required OrderBookDepth BookX { get; set; }
    public required OrderBookDepth BookY { get; set; }
    public double Beta { get; set; }
    public double Spread { get; set; }
    public double? ZScoreBuy { get; set; }
    public double? ZScoreSell { get; set; }
    public string Signal { get; set; } = "HOLD";
    public double HalfLifeSeconds { get; set; }
    public bool IsCointegrated { get; set; }
}
