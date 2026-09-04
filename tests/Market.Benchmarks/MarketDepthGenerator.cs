using Market.ApiClient.Dtos;

namespace Market.Benchmarks;

public class MarketDepthGenerator
{
    private readonly Random _random = new();

    // Настройки симулятора
    private readonly ushort _assetId;
    private readonly decimal _tickSize;
    private readonly int _depthLevels;

    // Текущее состояние рынка
    private decimal _currentMidPrice;

    public MarketDepthGenerator(ushort assetId, decimal initialPrice, decimal tickSize = 0.01m, int depthLevels = 10)
    {
        _assetId = assetId;
        _currentMidPrice = initialPrice;
        _tickSize = tickSize;
        _depthLevels = depthLevels;
    }

    /// <summary>
    /// Генерирует следующий «слепок» стакана с учетом изменения цены во времени.
    /// </summary>
    public MarketDepthDto GenerateNext()
    {
        // 1. Симулируем небольшое изменение средней цены (Случайное блуждание)
        // Цена может пойти вверх, вниз или остаться на месте
        decimal priceChangePercent = (decimal)(_random.NextDouble() * 2 - 1) * 0.0005m; // макс +-0.05%
        _currentMidPrice *= (1 + priceChangePercent);
        _currentMidPrice = RoundToTickSize(_currentMidPrice);

        // 2. Формируем спред (минимальный отступ для Bid и Ask от MidPrice)
        int halfSpreadTicks = _random.Next(1, 4); // спред от 2 до 6 шагов цены
        decimal bestBid = _currentMidPrice - (halfSpreadTicks * _tickSize);
        decimal bestAsk = _currentMidPrice + (halfSpreadTicks * _tickSize);

        // 3. Генерируем уровни
        var bids = new PriceLevelDto[_depthLevels];
        var asks = new PriceLevelDto[_depthLevels];

        for (int i = 0; i < _depthLevels; i++)
        {
            // Цены расходятся в разные стороны от спреда
            decimal bidPrice = bestBid - (i * _tickSize);
            decimal askPrice = bestAsk + (i * _tickSize);

            // Объемы генерируются на базе распределения, близкого к реальному рынке:
            // Базовый объем + случайный шум + периодические «крупные заявки» (плотности)
            bids[i] = new PriceLevelDto(bidPrice, GenerateRealisticVolume(i));
            asks[i] = new PriceLevelDto(askPrice, GenerateRealisticVolume(i));
        }

        // 4. Формируем таймstamp в микросекундах (Unix Epoch)
        long microsecondTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;

        return new MarketDepthDto(_assetId, microsecondTimestamp, bids, asks);
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

    private decimal RoundToTickSize(decimal price)
    {
        return Math.Round(price / _tickSize) * _tickSize;
    }
}
