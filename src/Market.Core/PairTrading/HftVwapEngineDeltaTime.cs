namespace Market.Core.PairTrading;

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

public class HftVwapEngineDeltaTime
{
    private double _beta;
    private double _pBeta = 1.0;
    
    // В HFT-моделях Q и R задаются на единицу времени (в секунду), а не на шаг!
    private const double QBetaPerSecond = 0.00005;
    private const double RBetaBase = 0.01;

    private readonly double _tradeVolumeY;
    private PositionState _currentPosition = PositionState.None;

    // Переменные для реализации Last Available Value и динамического dt
    private OrderBookDepth? _lastDepthX;
    private OrderBookDepth? _lastDepthY;
    private DateTime _lastEventTime = DateTime.MinValue;
    private DateTime _lastUpdateXTime = DateTime.MinValue;
    private DateTime _lastUpdateYTime = DateTime.MinValue;

    public HftVwapEngineDeltaTime(double initialBeta = 1.0, double tradeVolumeY = 1.0)
    {
        _beta = initialBeta;
        _tradeVolumeY = tradeVolumeY;
    }

    /// <summary>
    /// Вызывается асинхронно при обновлении стакана инструмента X
    /// </summary>
    public VwapResult? ProcessTickX(OrderBookDepth depthX, DateTime timestamp)
    {
        _lastDepthX = depthX;
        _lastUpdateXTime = timestamp;
        return ProcessTick(timestamp);
    }

    /// <summary>
    /// Вызывается асинхронно при обновлении стакана инструмента Y
    /// </summary>
    public VwapResult? ProcessTickY(OrderBookDepth depthY, DateTime timestamp)
    {
        _lastDepthY = depthY;
        _lastUpdateYTime = timestamp;
        return ProcessTick(timestamp);
    }

    private VwapResult? ProcessTick(DateTime timestamp)
    {
        // Пока не получили данные по обоим инструментам — расчет невозможен
        if (_lastDepthX == null || _lastDepthY == null)
        {
            _lastEventTime = timestamp;
            return null;
        }

        // 1. Расчет динамического шага времени (dt)
        double dt = 0;
        if (_lastEventTime != DateTime.MinValue)
        {
            dt = (timestamp - _lastEventTime).TotalSeconds;
        }
        _lastEventTime = timestamp;

        // Защита от одновременных или сетевых пакетов из одной микросекунды
        if (dt < 0.000001) dt = 0.000001;

        // 2. Считаем VWAP для Актива Y на основе жестко заданного объема лота
        double vwapAskY = _lastDepthY.GetVwap(_tradeVolumeY, isBuy: true);
        double vwapBidY = _lastDepthY.GetVwap(_tradeVolumeY, isBuy: false);
        double midVwapY = (vwapAskY + vwapBidY) / 2.0;

        // 3. Считаем объем для Актива X на основе текущей беты
        double requiredVolumeX = _tradeVolumeY / _beta;

        double vwapAskX = _lastDepthX.GetVwap(requiredVolumeX, isBuy: true);
        double vwapBidX = _lastDepthX.GetVwap(requiredVolumeX, isBuy: false);
        double midVwapX = (vwapAskX + vwapBidX) / 2.0;

        // --- ШАГ ПРЕДСКАЗАНИЯ КАЛМАНА (Predict) с учетом dt ---
        // Шум процесса Q масштабируется по времени
        _pBeta += QBetaPerSecond * dt;

        // --- ДИНАМИЧЕСКИЙ ШТРАФ STALE DATA (Корректировка R) ---
        // Считаем, как давно обновлялся каждый из стаканов относительно текущего события
        double timeSinceLastX = (timestamp - _lastUpdateXTime).TotalSeconds;
        double timeSinceLastY = (timestamp - _lastUpdateYTime).TotalSeconds;
        double maxStaleTime = Math.Max(timeSinceLastX, timeSinceLastY);
        
        // Чем старее данные одного из стаканов, тем сильнее мы раздуваем RBeta для текущего шага
        double rBetaDynamic = RBetaBase + (maxStaleTime * 0.05);

        // --- ШАГ ОБНОВЛЕНИЯ КАЛМАНА (Update) ---
        double sBeta = midVwapX * _pBeta * midVwapX + rBetaDynamic;
        double kBeta = (_pBeta * midVwapX) / sBeta;
        double midSpread = midVwapY - (_beta * midVwapX);
        _beta += kBeta * midSpread;
        _pBeta = (1.0 - kBeta * midVwapX) * _pBeta;

        // 4. Расчет торговых спредов по РЕАЛЬНЫМ ценам исполнения (VWAP)
        double spreadBuyY_SellX = vwapAskY - (_beta * vwapBidX);
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
            Signal = signal
        };
    }
}
