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

    public VwapResult ProcessSecond(OrderBookDepth x, OrderBookDepth y)
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

        return new VwapResult
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

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
public struct VwapResult
{
    public double Beta { get; set; }
    public double ZScoreBuy { get; set; }
    public double ZScoreSell { get; set; }
    public double VwapAskX { get; set; }
    public double VwapBidX { get; set; }
    public double VwapAskY { get; set; }
    public double VwapBidY { get; set; }
    public double VolumeX { get; set; }
    public double VolumeY { get; set; }

    public string Signal { get; set; }
}

#region СТРУКТУРЫ СТАКАНА
public struct OrderBookLevel
{
    public double Price { get; }
    public double Size { get; }
    public OrderBookLevel(double price, double size)
    {
        Price = price;
        Size = size;
    }
}

public class OrderBookDepth
{
    public List<OrderBookLevel> Bids { get; } // Покупатели (сортировка от лучшей/высшей к худшей)
    public List<OrderBookLevel> Asks { get; } // Продавцы (сортировка от лучшей/низшей к худшей)

    public OrderBookDepth(List<OrderBookLevel> bids, List<OrderBookLevel> asks)
    {
        Bids = bids;
        Asks = asks;
    }

    /// <summary>
    /// Расчет VWAP для проедания стакана на заданный объем
    /// </summary>
    public double GetVwap(double requiredVolume, bool isBuy)
    {
        var levels = isBuy ? Asks : Bids; // Если покупаем, бьем в лимиты продавцов (Asks), если продаем — в Bids
        double accumulatedVolume = 0;
        double accumulatedCash = 0;

        foreach (var level in levels)
        {
            double remainingVolume = requiredVolume - accumulatedVolume;
            if (remainingVolume <= 0)
            {
                break;
            }

            double volumeToTake = Math.Min(remainingVolume, level.Size);
            accumulatedCash += volumeToTake * level.Price;
            accumulatedVolume += volumeToTake;
        }

        // Если в стакане не хватило ликвидности для нашего объема, возвращаем худшую цену или выкидываем ошибку
        if (accumulatedVolume < requiredVolume)
        {
            return levels[^1].Price; // Худшая доступная цена (рыночное проскальзывание)
        }

        return accumulatedCash / requiredVolume;
    }
}
#endregion
