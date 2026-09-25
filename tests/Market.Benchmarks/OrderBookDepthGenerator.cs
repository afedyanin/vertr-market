using Market.Contracts.Models;

namespace Market.Benchmarks;

public class OrderBookDepthGenerator
{
    private readonly Random _random = new();

    // Настройки симулятора
    private readonly ushort _assetId;
    private readonly double _tickSize;
    private readonly int _depthLevels;

    // Текущее состояние рынка
    private double _currentMidPrice;

    public OrderBookDepthGenerator(ushort assetId, double initialPrice, double tickSize = 0.01, int depthLevels = 10)
    {
        _assetId = assetId;
        _currentMidPrice = initialPrice;
        _tickSize = tickSize;
        _depthLevels = depthLevels;
    }

    /// <summary>
    /// Генерирует следующий «слепок» стакана с учетом изменения цены во времени.
    /// </summary>
    public OrderBookDepth GenerateNext()
    {
        // 1. Симулируем небольшое изменение средней цены (Случайное блуждание)
        // Цена может пойти вверх, вниз или остаться на месте
        var priceChangePercent = (double)(_random.NextDouble() * 2 - 1) * 0.0005; // макс +-0.05%
        _currentMidPrice *= (1 + priceChangePercent);
        _currentMidPrice = RoundToTickSize(_currentMidPrice);

        // 2. Формируем спред (минимальный отступ для Bid и Ask от MidPrice)
        int halfSpreadTicks = _random.Next(1, 4); // спред от 2 до 6 шагов цены
        var bestBid = _currentMidPrice - (halfSpreadTicks * _tickSize);
        var bestAsk = _currentMidPrice + (halfSpreadTicks * _tickSize);

        // 3. Генерируем уровни
        var bids = new OrderBookLevel[_depthLevels];
        var asks = new OrderBookLevel[_depthLevels];

        for (int i = 0; i < _depthLevels; i++)
        {
            // Цены расходятся в разные стороны от спреда
            var bidPrice = bestBid - (i * _tickSize);
            var askPrice = bestAsk + (i * _tickSize);

            // Объемы генерируются на базе распределения, близкого к реальному рынке:
            // Базовый объем + случайный шум + периодические «крупные заявки» (плотности)
            bids[i] = new OrderBookLevel(bidPrice, GenerateRealisticVolume(i));
            asks[i] = new OrderBookLevel(askPrice, GenerateRealisticVolume(i));
        }

        return new OrderBookDepth(_assetId, bids, asks);
    }

    public static Dictionary<ushort, OrderBookDepthGenerator> InitGenerators(int count)
    {
        var res = new Dictionary<ushort, OrderBookDepthGenerator>();
        ushort assetId = 1000;

        for (var i = 0; i < count; i++)
        {
            res[assetId] = new OrderBookDepthGenerator(assetId, 100 + Random.Shared.Next(200));
            assetId++;
        }

        return res;
    }

    private uint GenerateRealisticVolume(int levelIndex)
    {
        // Базовый объем (ближе к спреду ликвидность обычно выше, но с шумом)
        double baseVolume = _random.Next(10, 150);

        // Добавляем экспоненциальное затухание или случайные «прострелы» (крупные лимитки)
        if (_random.NextDouble() > 0.92)
        {
            baseVolume *= _random.Next(5, 15); // Крупная "институциональная" плотность в стакане
        }
        else
        {
            // Небольшое затухание объема вглубь стакана для реалистичности
            baseVolume *= Math.Exp(-levelIndex * 0.05);
        }

        return (uint)Math.Max(1, Math.Round(baseVolume));
    }

    private double RoundToTickSize(double price) => Math.Round(price / _tickSize) * _tickSize;
}
