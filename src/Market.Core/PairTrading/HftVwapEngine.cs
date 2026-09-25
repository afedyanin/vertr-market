using Market.ApiClient.Models;
using Market.Core.Extensions;

namespace Market.Core.PairTrading;

/*
1. Динамический пересчет объема X: Так как соотношение цен меняется, 
для удержания рыночно-нейтральной позиции объем второго  актива должен рассчитываться как Volume_Y / Beta. 
Движок запрашивает стакан X именно под этот скорректированный объем.

2. Проедание стакана (Метод GetVwap): Алгоритм последовательно суммирует объемы (level.Size) уровней стакана. 
Если на первом уровне доступно только 1.5 лота, а нам нужно 5, он берет 1.5 лота по лучшей цене, 
затем переходит на второй уровень, берет остаток там, и выводит средневзвешенную цену чистой покупки.

3. Безопасность Калмана: Фильтр обучается на Mid-VWAP (среднее между VWAP покупки и VWAP продажи). 
Это гарантирует, что модель не сдвинет внутреннее математическое ожидание беты из-за того, 
что на одной из бирж временно расширился спред.

*/

public class HftVwapEngine
{
    private double _beta;
    private double _pBeta = 1.0;
    private const double QBeta = 0.00005;
    private const double RBeta = 0.01;

    private readonly double _tradeVolumeY;
    private PositionState _currentPosition = PositionState.None;

    public HftVwapEngine(double initialBeta = 1.0, double tradeVolumeY = 1.0)
    {
        _beta = initialBeta;
        _tradeVolumeY = tradeVolumeY;
    }

    public PairTradingSignal ProcessSecond(OrderBookDepth x, OrderBookDepth y)
    {
        // 1. Считаем VWAP для Актива Y на основе жестко заданного объема торгового лота
        double vwapAskY = y.GetVwap(_tradeVolumeY, isBuy: true);
        double vwapBidY = y.GetVwap(_tradeVolumeY, isBuy: false);
        double midVwapY = (vwapAskY + vwapBidY) / 2.0;

        // 2. Считаем объем для Актива X на основе текущей беты: Volume_X = Volume_Y / Beta
        double requiredVolumeX = _tradeVolumeY / _beta;

        double vwapAskX = x.GetVwap(requiredVolumeX, isBuy: true);
        double vwapBidX = x.GetVwap(requiredVolumeX, isBuy: false);
        double midVwapX = (vwapAskX + vwapBidX) / 2.0;

        // 3. Обновляем Бету фильтра Калмана по средним VWAP ценам (Mid VWAP)
        _pBeta += QBeta;
        double sBeta = midVwapX * _pBeta * midVwapX + RBeta;
        double kBeta = (_pBeta * midVwapX) / sBeta;
        double midSpread = midVwapY - (_beta * midVwapX);
        _beta += kBeta * midSpread;
        _pBeta = (1.0 - kBeta * midVwapX) * _pBeta;

        // 4. Расчет торговых спредов по РЕАЛЬНЫМ ценам исполнения (VWAP)
        // Сигнал Buy Y / Short X -> Покупка Y по VWAP Ask, Продажа X по VWAP Bid
        double spreadBuyY_SellX = vwapAskY - (_beta * vwapBidX);

        // Сигнал Short Y / Long X -> Продажа Y по VWAP Bid, Покупка X по VWAP Ask
        double spreadSellY_BuyX = vwapBidY - (_beta * vwapAskX);

        double sigma = Math.Sqrt(sBeta);
        double zScoreBuy = spreadBuyY_SellX / sigma;
        double zScoreSell = spreadSellY_BuyX / sigma;

        // 5. Логика генерации сигналов
        string signal = "HOLD";

        if (_currentPosition == PositionState.None)
        {
            if (zScoreSell > 2.0)
            {
                signal = $"EXECUTE SHORT Y (VWAP: {vwapBidY:F2}) / LONG X (VWAP: {vwapAskX:F2})";
                _currentPosition = PositionState.ShortY_LongX;
            }
            else if (zScoreBuy < -2.0)
            {
                signal = $"EXECUTE LONG Y (VWAP: {vwapAskY:F2}) / SHORT X (VWAP: {vwapBidX:F2})";
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

        return new PairTradingSignal
        {
            Beta = _beta,
            ZScoreBuy = zScoreBuy,
            ZScoreSell = zScoreSell,
            VwapAskX = vwapAskX,
            VwapBidX = vwapBidX,
            VwapAskY = vwapAskY,
            VwapBidY = vwapBidY,
            VolumeX = requiredVolumeX,
            VolumeY = _tradeVolumeY,
            Signal = signal,
        };
    }
}
