using Market.Core.Algos.Common;
using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

public class KalmanHalfLifeMidPriceAlgo : PairTradingAlgoBase
{
    // Калман 2: Онлайн-оценка AR(1) для спреда (Модель Орнштейна-Уленбека)
    // Момент входа в позицию (по таймстампам стаканов) — для тайм-стопа по полураспаду.
    private DateTime? _entryTime = null;

    private readonly double _entryThreshold;
    private readonly double _exitThreshold;
    private readonly decimal _requiredYQty;

    private readonly KalmanFilterEstimator _kalmanFilter;
    private readonly CointegrationEstimator _cointegration;

    public KalmanHalfLifeMidPriceAlgo(AlgoMetadata metadata) : base(metadata)
    {
        _requiredYQty = (decimal)AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.RequiredYQty, 1.0);
        _entryThreshold = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.EntryThreshold, 2.0);
        _exitThreshold = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.ExitThreshold, 0.2);

        var initialBeta = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.InitialBeta, 1.0);
        var initialErrorCovariance = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.InitialErrorCovariance, 1.0);
        var q = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.Q, 0.00005);
        var r = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.R, 0.01);

        _kalmanFilter = new KalmanFilterEstimator(
            initialBeta: initialBeta,
            initialErrorCovariance: initialErrorCovariance,
            q: q,
            r: r);

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
        _cointegration.Update(_kalmanFilter.Spread);

        var sigma = _kalmanFilter.Sigma;
        var beta = _kalmanFilter.Beta;

        // РАСЧЕТ РЕАЛЬНЫХ ТОРГОВЫХ СПРЕДОВ С УЧЕТОМ BID/ASK
        // Спред, если мы заходим в LONG Y и SHORT X (Покупаем Y по Ask, продаем X по Bid)
        var spreadBuyY_SellX = _lastDepthY!.BestAsk - (beta * _lastDepthX!.BestBid);
        // Спред, если мы заходим в SHORT Y и LONG X (Продаем Y по Bid, покупаем X по Ask)
        var spreadSellY_BuyX = _lastDepthY.BestBid - (beta * _lastDepthX.BestAsk);

        var zScoreBuy = spreadBuyY_SellX / sigma;
        var zScoreSell = spreadSellY_BuyX / sigma;

        // Защита от деления на ноль и «отравления» сигнала значениями NaN/Infinity.
        if (!zScoreBuy.HasValue ||
            !double.IsFinite(zScoreBuy.Value) ||
            !zScoreSell.HasValue ||
            !double.IsFinite(zScoreSell.Value))
        {
            return null;
        }

        (var xQty, var yQty) = GetQty(zScoreBuy, zScoreSell);

        if (xQty == 0 && yQty == 0)
        {
            return null;
        }

        var signal = new PairTradingSignal
        {
            //SourceId = $"{nameof(KalmanHalfLifeMidPriceAlgo)}:{AssetX}:{AssetY}",
            StrategyId = StrategyId,
            BookX = _lastDepthX,
            BookY = _lastDepthY,
            XQty = xQty,
            YQty = yQty,
        };

        signal.Params[SignalParamKeys.PriceX] = priceX;
        signal.Params[SignalParamKeys.PriceY] = priceY;
        signal.Params[SignalParamKeys.Beta] = _kalmanFilter.Beta;
        signal.Params[SignalParamKeys.Spread] = _kalmanFilter.Spread;
        signal.Params[SignalParamKeys.Sigma] = _kalmanFilter.Sigma;
        signal.Params[SignalParamKeys.ZScoreBuy] = zScoreBuy!.Value;
        signal.Params[SignalParamKeys.ZScoreSell] = zScoreSell!.Value;
        signal.Params[SignalParamKeys.HalfLifeSeconds] = _cointegration.HalfLife;
        signal.Params[SignalParamKeys.IsCointegrated] = _cointegration.IsCointegrated ? 1 : 0;

        _openXQty += xQty;
        _openYQty += yQty;
        _entryTime = HasOpenPosition ? LatestTimestamp() : null;

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


    private (decimal xQty, decimal yQty) GetQty(double? zScoreBuy, double? zScoreSell)
    {
        var beta = _kalmanFilter.Beta;
        var isCointegrated = _cointegration.IsCointegrated;
        var halfLife = _cointegration.HalfLife;

        var xQty = 0m;
        var yQty = 0m;

        if (!HasOpenPosition && isCointegrated && halfLife > 1.0 && halfLife < 60.0)
        {
            // Вход в Short Y / Long X имеет смысл, только если спред ПРОДАЖИ (Sell) превысил верхний порог.
            // Мы будем бить по стакану: продавать Y по Bid и покупать X по Ask.
            if (zScoreSell > _entryThreshold)
            {
                yQty = -_requiredYQty;
                xQty = _requiredYQty / (decimal)beta;
            }
            // Вход в Long Y / Short X: покупаем Y по Ask, продаем X по Bid.
            else if (zScoreBuy < -_entryThreshold)
            {
                yQty = _requiredYQty;
                xQty = -_requiredYQty / (decimal)beta;
            }
        }
        else if (!isCointegrated)
        {
            // Выход: коинтеграция полностью исчезла
            yQty = -_openYQty;
            xQty = -_openXQty;
        }
        else if (SecondsInTrade() > (halfLife * 2.5))
        {
            // Выход: позиция висит дольше, чем 2-3 периода полураспада
            yQty = -_openYQty;
            xQty = -_openXQty;
        }
        else if (_openYQty < 0 && zScoreBuy <= _exitThreshold)
        {
            // Выход из позиции Short Y / Long X означает, что нам нужно КУПИТЬ Y (по Ask) и ПРОДАТЬ X (по Bid).
            // Поэтому мы отслеживаем zScoreBuy для выхода.
            yQty = -_openYQty;
            xQty = -_openXQty;
        }
        else if (_openYQty > 0 && zScoreSell >= -_exitThreshold)
        {
            // Выход из позиции Long Y / Short X: ПРОДАЕМ Y (по Bid) и ПОКУПАЕМ X (по Ask).
            yQty = -_openYQty;
            xQty = -_openXQty;
        }

        return (xQty, yQty);
    }
}
