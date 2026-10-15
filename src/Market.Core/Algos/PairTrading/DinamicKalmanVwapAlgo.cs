using Market.Core.Algos.Common;
using Market.Core.Extensions;
using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

/*
1. Динамический пересчет объема X: Так как соотношение цен меняется, 
для удержания рыночно-нейтральной позиции объем второго актива должен рассчитываться как Volume_Y / Beta. 
Движок запрашивает стакан X именно под этот скорректированный объем.

2. Проедание стакана (Метод GetVwap): Алгоритм последовательно суммирует объемы (level.Size) уровней стакана. 
Если на первом уровне доступло только 1.5 лота, а нам нужно 5, он берет 1.5 лота по лучшей цене, 
затем переходит на второй уровень, берет остаток там, и выводит средневзвешенную цену чистой покупки.

3. Безопасность Калмана: Фильтр обучается на Mid-VWAP (среднее между VWAP покупки и VWAP продажи). 
Это гарантирует, что модель не сдвинет внутреннее математическое ожидание беты из-за того, 
что на одной из бирж временно расширился спред.

4. СОБЫТИЙНЫЙ ДИНАМИЧЕСКИЙ КАЛМАН (LAV): Движок принимает асинхронные тики любого из инструментов, 
хранит последнее состояние стакана партнера, вычисляет реальный dt между событиями для масштабирования QBeta 
и накладывает штраф за "устаревание" данных на RBeta.
*/

public class DinamicKalmanVwapAlgo : PairTradingAlgoBase
{
    private const double EntryThreshold = 2.0;
    private const double ExitThreshold = 0.2;
    private readonly double _requiredYQty = 1.0;

    private readonly DynamicKalmanFilterEstimator _kalmanFilter;
    private readonly double _feeRateX;
    private readonly double _feeRateY;
    private Quote? _quoteX;
    private Quote? _quoteY;

    public DinamicKalmanVwapAlgo(
        ushort assetX,
        ushort assetY,
        double initialBeta = 1.0,
        double tradeVolumeY = 1.0,
        double feeRateX = 0.0,
        double feeRateY = 0.0) : base(assetX, assetY)
    {
        _kalmanFilter = new DynamicKalmanFilterEstimator(initialBeta);
        _feeRateX = feeRateX;
        _feeRateY = feeRateY;
    }

    protected override IPairTradingSignal? ProcessTickX()
    {
        if (_lastDepthX is null)
        {
            return null;
        }

        var requiredVolumeX = _requiredYQty / _kalmanFilter.Beta;
        _quoteX = _lastDepthX.GetVwapQuote(requiredVolumeX);

        // Пустой/неликвидный стакан даёт NaN — не пускаем его в фильтр.
        if (!IsValidQuote(_quoteX))
        {
            return null;
        }

        _kalmanFilter.UpdateX(_quoteX.Mid, _lastDepthX.Timestamp);
        return ProcessTick();
    }

    protected override IPairTradingSignal? ProcessTickY()
    {
        if (_lastDepthY is null)
        {
            return null;
        }

        _quoteY = _lastDepthY.GetVwapQuote(_requiredYQty);

        if (!IsValidQuote(_quoteY))
        {
            return null;
        }

        _kalmanFilter.UpdateY(_quoteY.Mid, _lastDepthY.Timestamp);
        return ProcessTick();
    }


    private IPairTradingSignal? ProcessTick()
    {
        if (!_kalmanFilter.HasValidData)
        {
            return null;
        }

        var beta = _kalmanFilter.Beta;
        var sigma = _kalmanFilter.Sigma;

        // Расчет торговых спредов по РЕАЛЬНЫМ ценам исполнения (VWAP)
        var spreadBuyY_SellX = _quoteY!.Ask - (beta * _quoteX!.Bid);
        var spreadSellY_BuyX = _quoteY!.Bid - (beta * _quoteX!.Ask);
        var zScoreBuy = spreadBuyY_SellX / sigma;
        var zScoreSell = spreadSellY_BuyX / sigma;

        // Защита от деления на ноль и «отравления» сигнала значениями NaN/Infinity.
        if (!double.IsFinite(zScoreBuy) || !double.IsFinite(zScoreSell))
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
            SourceId = $"{nameof(DinamicKalmanVwapAlgo)}:{AssetX}:{AssetY}",
            BookX = _lastDepthX!,
            BookY = _lastDepthY!,
            XQty = xQty,
            YQty = yQty,
        };

        signal.Params[SignalParamKeys.Beta] = _kalmanFilter.Beta;
        signal.Params[SignalParamKeys.Spread] = _kalmanFilter.Spread;
        signal.Params[SignalParamKeys.Sigma] = _kalmanFilter.Sigma;
        signal.Params[SignalParamKeys.ZScoreBuy] = zScoreBuy;
        signal.Params[SignalParamKeys.ZScoreSell] = zScoreSell;

        return signal;
    }

    private (decimal xQty, decimal yQty) GetQty(double? zScoreBuy, double? zScoreSell)
    {
        var beta = _kalmanFilter.Beta;

        var xQty = 0.0m;
        var yQty = 0.0m;

        // Комиссии уменьшают фактический edge входа: переводим их в единицы Z-Score
        // и ужесточаем порог входа на эту величину (при нулевых ставках фильтр выключен).
        var feesInZScore = CalculateFeesInZScore(beta);

        var requiredYQty = (decimal)_requiredYQty;
        if (!HasOpenPosition)
        {
            // Вход в Short Y / Long X имеет смысл, только если спред ПРОДАЖИ (Sell) превысил верхний порог.
            // Мы будем бить по стакану: продавать Y по Bid и покупать X по Ask.
            if (zScoreSell - feesInZScore > EntryThreshold)
            {
                yQty = (-requiredYQty);
                xQty = requiredYQty / (decimal)beta;
            }
            // Вход в Long Y / Short X: покупаем Y по Ask, продаем X по Bid.
            else if (zScoreBuy + feesInZScore < -EntryThreshold)
            {
                yQty = requiredYQty;
                xQty = -requiredYQty / (decimal)beta;
            }
        }
        else if (_openYQty < 0 && zScoreBuy <= ExitThreshold)
        {
            // Выход из позиции Short Y / Long X означает, что нам нужно КУПИТЬ Y (по Ask) и ПРОДАТЬ X (по Bid).
            // Поэтому мы отслеживаем zScoreBuy для выхода.
            yQty = -_openYQty;
            xQty = -_openXQty;
        }
        else if (_openYQty > 0 && zScoreSell >= -ExitThreshold)
        {
            // Выход из позиции Long Y / Short X: ПРОДАЕМ Y (по Bid) и ПОКУПАЕМ X (по Ask).
            yQty = -_openYQty;
            xQty = -_openXQty;
        }

        return (xQty, yQty);
    }

    /// <summary>
    /// Стоимость комиссий «вход + выход» в единицах Z-Score для текущего спреда.
    /// </summary>
    private double CalculateFeesInZScore(double beta)
    {
        if (_feeRateX <= 0 && _feeRateY <= 0)
        {
            return 0.0;
        }

        var sigma = _kalmanFilter.Sigma;
        if (!(sigma > 0))
        {
            return 0.0;
        }

        var midX = (_quoteX!.Bid + _quoteX.Ask) / 2.0;
        var midY = (_quoteY!.Bid + _quoteY.Ask) / 2.0;

        var totalFeesInY = 2.0 * (midY * _feeRateY) + 2.0 * beta * (midX * _feeRateX);
        return totalFeesInY / sigma;
    }

    private static bool IsValidQuote(Quote quote)
        => double.IsFinite(quote.Bid) && double.IsFinite(quote.Ask) && quote.Ask > 0 && quote.Bid > 0;
}
