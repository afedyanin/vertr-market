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

    private readonly DynamicKalmanFilterEstimator _kalmanFilter;
    private readonly double _tradeVolumeY;
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
        _tradeVolumeY = tradeVolumeY;
        _feeRateX = feeRateX;
        _feeRateY = feeRateY;
    }

    protected override IPairTradingSignal? ProcessTickX()
    {
        if (_lastDepthX is null)
        {
            return null;
        }

        var requiredVolumeX = _tradeVolumeY / _kalmanFilter.Beta;
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

        _quoteY = _lastDepthY.GetVwapQuote(_tradeVolumeY);

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

        // Комиссии уменьшают фактический edge входа: переводим их в единицы Z-Score
        // и ужесточаем порог входа на эту величину (при нулевых ставках фильтр выключен).
        var feesInZScore = CalculateFeesInZScore(beta);

        // 5. Логика генерации сигналов
        var action = PairTradingAction.Hold;

        if (_currentState == StrategyState.None)
        {
            // Вход оцениваем по спреду ИСПОЛНЯЕМОЙ ноги (как в KalmanHalfLifeMidPriceAlgo и HftFeesEngine):
            //  - ShortY_LongX исполняется продажей Y (Bid) и покупкой X (Ask) → zScoreSell;
            //  - LongY_ShortX исполняется покупкой Y (Ask) и продажей X (Bid) → zScoreBuy.
            if (zScoreSell - feesInZScore > EntryThreshold)
            {
                action = PairTradingAction.ShortY_LongX;
                _currentState = StrategyState.ShortY_LongX;
            }
            else if (zScoreBuy + feesInZScore < -EntryThreshold)
            {
                action = PairTradingAction.LongY_ShortX;
                _currentState = StrategyState.LongY_ShortX;
            }
        }
        else if (_currentState == StrategyState.ShortY_LongX && zScoreBuy <= ExitThreshold)
        {
            action = PairTradingAction.Exit;
            _currentState = StrategyState.None;
        }
        else if (_currentState == StrategyState.LongY_ShortX && zScoreSell >= -ExitThreshold)
        {
            action = PairTradingAction.Exit;
            _currentState = StrategyState.None;
        }

        var xQty = beta > 0 ? _tradeVolumeY / beta : 0.0;

        var signal = new PairTradingSignal
        {
            SourceId = $"{nameof(DinamicKalmanVwapAlgo)}:{AssetX}:{AssetY}",
            Action = action,
            BookX = _lastDepthX!,
            BookY = _lastDepthY!,
            YQty = (decimal)_tradeVolumeY,
            XQty = double.IsFinite(xQty) ? (decimal)xQty : 0m,
        };

        signal.Params[SignalParamKeys.Beta] = _kalmanFilter.Beta;
        signal.Params[SignalParamKeys.Spread] = _kalmanFilter.Spread;
        signal.Params[SignalParamKeys.ZScoreBuy] = zScoreBuy;
        signal.Params[SignalParamKeys.ZScoreSell] = zScoreSell;

        return signal;
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
