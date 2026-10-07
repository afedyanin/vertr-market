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

public class VwapDeltaTimeAlgo : PairTradingAlgoBase
{
    private readonly KalmanFilterDynamicEstimator _kalmanFilter;
    private readonly double _tradeVolumeY;

    private Quote? _quoteX;
    private Quote? _quoteY;

    public VwapDeltaTimeAlgo(
        ushort assetX,
        ushort assetY,
        double initialBeta = 1.0,
        double tradeVolumeY = 1.0) : base(assetX, assetY)
    {
        _kalmanFilter = new KalmanFilterDynamicEstimator(initialBeta);
        _tradeVolumeY = tradeVolumeY;
    }

    protected override ITradingSignal? ProcessTickX()
    {
        if (_lastDepthX is null)
        {
            return null;
        }

        var requiredVolumeX = _tradeVolumeY / _kalmanFilter.Beta;
        _quoteX = _lastDepthX.GetVwapQuote(requiredVolumeX);
        _kalmanFilter.UpdateX(_quoteX.Mid, _lastDepthX.Timestamp);
        return ProcessTick();
    }

    protected override ITradingSignal? ProcessTickY()
    {
        if (_lastDepthY is null)
        {
            return null;
        }

        _quoteY = _lastDepthY.GetVwapQuote(_tradeVolumeY);
        _kalmanFilter.UpdateY(_quoteY.Mid, _lastDepthY.Timestamp);
        return ProcessTick();
    }


    private ITradingSignal? ProcessTick()
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

        // 5. Логика генерации сигналов
        string signal = "HOLD";

        if (_currentPosition == PositionState.None)
        {
            if (zScoreBuy > 2.0)
            {
                //signal = $"EXECUTE SHORT Y (VWAP: {vwapBidY:F2}) / LONG X (VWAP: {vwapAskX:F2})";
                signal = $"EXECUTE SHORT Y / LONG X";
                _currentPosition = PositionState.ShortY_LongX;
            }
            else if (zScoreSell < -2.0)
            {
                // signal = $"EXECUTE LONG Y (VWAP: {vwapAskY:F2}) / SHORT X (VWAP: {vwapBidX:F2})";
                signal = $"EXECUTE LONG Y / SHORT X";
                _currentPosition = PositionState.LongY_ShortX;
            }
        }
        else if (_currentPosition == PositionState.ShortY_LongX && zScoreBuy <= 0.2)
        {
            signal = $"EXIT POSITION (TAKE PROFIT)";
            _currentPosition = PositionState.None;
        }
        else if (_currentPosition == PositionState.LongY_ShortX && zScoreSell >= -0.2)
        {
            signal = $"EXIT POSITION (TAKE PROFIT)";
            _currentPosition = PositionState.None;
        }

        var requiredVolumeX = _tradeVolumeY / _kalmanFilter.Beta;

        return new PairTradingSignal
        {
            Beta = _kalmanFilter.Beta,
            Signal = signal,
            AssetX = AssetX,
            AssetY = AssetY,
            ZScoreBuy = zScoreBuy,
            ZScoreSell = zScoreSell,
            VolumeX = requiredVolumeX,
            VolumeY = _tradeVolumeY,
            VwapX = _quoteX,
            VwapY = _quoteY,
        };
    }
}
