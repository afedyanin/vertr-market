using Market.Core.Algos.Common;
using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

public class KalmanHalfLifeMidPriceAlgo : PairTradingAlgoBase
{
    // Калман 2: Онлайн-оценка AR(1) для спреда (Модель Орнштейна-Уленбека)
    // Момент входа в позицию (по таймстампам стаканов) — для тайм-стопа по полураспаду.
    private DateTime? _entryTime = null;

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
        var action = PairTradingAction.Hold;

        if (_currentState == StrategyState.None && isCointegrated && halfLife > 1.0 && halfLife < 60.0)
        {
            // Вход в Short Y / Long X имеет смысл, только если спред ПРОДАЖИ (Sell) превысил верхний порог.
            // Мы будем бить по стакану: продавать Y по Bid и покупать X по Ask.
            if (zScoreSell > 2.0)
            {
                action = PairTradingAction.ShortY_LongX;
                _currentState = StrategyState.ShortY_LongX;
                _entryTime = LatestTimestamp();
            }
            // Вход в Long Y / Short X: покупаем Y по Ask, продаем X по Bid.
            else if (zScoreBuy < -2.0)
            {
                action = PairTradingAction.LongY_ShortX;
                _currentState = StrategyState.LongY_ShortX;
                _entryTime = LatestTimestamp();
            }
        }
        else if (_currentState == StrategyState.ShortY_LongX)
        {
            // СИГНАЛ ВЫХОДА ПО ПОЛУРАСПАДУ (Time Stop): 
            // Если позиция висит дольше, чем 2-3 периода полураспада, математическое ожидание прибыли исчезает.
            // Либо если коинтеграция полностью исчезла (isCointegrated == false)
            // Выход из позиции Short Y / Long X означает, что нам нужно КУПИТЬ Y (по Ask) и ПРОДАТЬ X (по Bid).
            // Поэтому мы отслеживаем zScoreBuy для выхода.
            if (zScoreBuy <= 0.2 || !isCointegrated || SecondsInTrade() > (halfLife * 2.5))
            {
                action = PairTradingAction.Exit;
                _currentState = StrategyState.None;
                _entryTime = null;
            }
        }
        else if (_currentState == StrategyState.LongY_ShortX)
        {
            // Выход из позиции Long Y / Short X: ПРОДАЕМ Y (по Bid) и ПОКУПАЕМ X (по Ask).
            if (zScoreSell >= -0.2 || !isCointegrated || SecondsInTrade() > (halfLife * 2.5))
            {
                action = PairTradingAction.Exit;
                _currentState = StrategyState.None;
                _entryTime = null;
            }
        }

        var signal = new PairTradingSignal
        {
            SourceId = $"{nameof(KalmanHalfLifeMidPriceAlgo)}:{AssetX}:{AssetY}",
            Action = action,
            BookX = _lastDepthX,
            BookY = _lastDepthY,
        };

        signal.Params[SignalParamKeys.PriceX] = priceX;
        signal.Params[SignalParamKeys.PriceY] = priceY;
        signal.Params[SignalParamKeys.Beta] = beta;
        signal.Params[SignalParamKeys.Spread] = spread;
        signal.Params[SignalParamKeys.ZScoreBuy] = zScoreBuy!.Value;
        signal.Params[SignalParamKeys.ZScoreSell] = zScoreSell!.Value;
        signal.Params[SignalParamKeys.HalfLifeSeconds] = halfLife;
        signal.Params[SignalParamKeys.IsCointegrated] = isCointegrated ? 1 : 0;

        return signal;
    }

    /// <summary>
    /// Актуальное время пары — максимальный из таймстампов последних стаканов X и Y.
    /// </summary>
    private DateTime LatestTimestamp()
        => _lastDepthX!.Timestamp >= _lastDepthY!.Timestamp ? _lastDepthX.Timestamp : _lastDepthY.Timestamp;

    /// <summary>
    /// Время удержания позиции в секундах (по таймстампам стаканов, а не по числу тиков).
    /// </summary>
    private double SecondsInTrade()
        => _entryTime.HasValue ? (LatestTimestamp() - _entryTime.Value).TotalSeconds : 0.0;
}
