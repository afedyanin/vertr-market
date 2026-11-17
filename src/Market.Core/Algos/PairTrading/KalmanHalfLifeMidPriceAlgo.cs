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

    private readonly KalmanFilterEstimator _kalmanFilter;
    private readonly CointegrationEstimator _cointegration;

    private readonly int _lotSizeX;
    private readonly int _lotSizeY;
    private readonly int _maxLots;


    public KalmanHalfLifeMidPriceAlgo(AlgoMetadata metadata) : base(metadata)
    {
        _entryThreshold = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.EntryThreshold, 2.0);
        _exitThreshold = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.ExitThreshold, 0.2);
        _lotSizeX = (int)AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.LotSizeX, 1.0);
        _lotSizeY = (int)AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.LotSizeY, 1.0);
        _maxLots = (int)AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.MaxLots, 10);

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
            XQtyLots = xQty,
            YQtyLots = yQty,
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

        _openXQtyLots += xQty;
        _openYQtyLots += yQty;
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


    private (int xQty, int yQty) GetQty(double? zScoreBuy, double? zScoreSell)
    {
        var isCointegrated = _cointegration.IsCointegrated;
        var halfLife = _cointegration.HalfLife;

        var xQty = 0;
        var yQty = 0;

        if (!HasOpenPosition && isCointegrated && halfLife > 1.0 && halfLife < 60.0)
        {

            (xQty, yQty) = ApproximateLotCalculator.CalculateLots(
                _kalmanFilter.Beta,
                _lotSizeX,
                _lotSizeY,
                _maxLots);

            // Вход в Short Y / Long X имеет смысл, только если спред ПРОДАЖИ (Sell) превысил верхний порог.
            // Мы будем бить по стакану: продавать Y по Bid и покупать X по Ask.
            if (zScoreSell > _entryThreshold)
            {
                return (xQty, -yQty);
            }
            // Вход в Long Y / Short X: покупаем Y по Ask, продаем X по Bid.
            else if (zScoreBuy < -_entryThreshold)
            {
                return (-xQty, yQty);
            }
        }
        else if (!isCointegrated)
        {
            // Выход: коинтеграция полностью исчезла
            yQty = -_openYQtyLots;
            xQty = -_openXQtyLots;
        }
        else if (SecondsInTrade() > (halfLife * 2.5))
        {
            // Выход: позиция висит дольше, чем 2-3 периода полураспада
            yQty = -_openYQtyLots;
            xQty = -_openXQtyLots;
        }
        else if (_openYQtyLots < 0 && zScoreBuy <= _exitThreshold)
        {
            // Выход из позиции Short Y / Long X означает, что нам нужно КУПИТЬ Y (по Ask) и ПРОДАТЬ X (по Bid).
            // Поэтому мы отслеживаем zScoreBuy для выхода.
            yQty = -_openYQtyLots;
            xQty = -_openXQtyLots;
        }
        else if (_openYQtyLots > 0 && zScoreSell >= -_exitThreshold)
        {
            // Выход из позиции Long Y / Short X: ПРОДАЕМ Y (по Bid) и ПОКУПАЕМ X (по Ask).
            yQty = -_openYQtyLots;
            xQty = -_openXQtyLots;
        }

        return (xQty, yQty);
    }
}
