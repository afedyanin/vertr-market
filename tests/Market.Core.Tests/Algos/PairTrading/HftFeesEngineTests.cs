using Market.Core.Algos.PairTrading;
using Market.Core.Models;

namespace Market.Core.Tests.Algos.PairTrading;

public class HftFeesEngineTests
{
    [Test]
    public void HftFeesEngineSample()
    {
        // Настройки комиссий: например, 0.04% (0.0004) для Актива X и 0.05% (0.0005) для Актива Y
        double feeRateX = 0.0004;
        double feeRateY = 0.0005;
        double tradeVolumeY = 5.0;

        var engine = new HftFeesEngine(initialBeta: 1.5, tradeVolumeY: tradeVolumeY, feeRateX: feeRateX, feeRateY: feeRateY);

        // Стакан X (Цена, Объем)
        var orderBookX = new OrderBookDepth(
            DateTime.UtcNow,
            1,
            bids: [new(100.0, 10.0)],
            asks: [new(100.1, 10.0)]
        );

        // Стакан Y: Симулируем ситуацию, где спред расширился (Z-Score высокий), 
        // но из-за комиссий алгоритм должен проигнорировать этот вход
        var orderBookY = new OrderBookDepth(
            DateTime.UtcNow,
            2,
            bids: [new(151.2, 10.0)],
            asks: [new(151.5, 10.0)]
        );

        var r = engine.ProcessSecond(orderBookX, orderBookY);

        Console.WriteLine($"Чистый Z-Score Sell (Без комиссий): {r.RawZScoreSell:F2}");
        Console.WriteLine($"Скорректированный Z-Score Sell (С учетом комиссий): {r.ZScoreSellWithFees:F2}");
        Console.WriteLine($"Суммарная стоимость комиссий на круг (в единицах Y): {r.TotalFeesInY:F4}");
        Console.WriteLine($"Сигнал: {r.Signal}");
    }
}
