using Market.Core.Algos.PairTrading;

namespace Market.Core.Tests.Algos.PairTrading;

public class KalmanPairsEngineTests
{
    [Test]
    public void KalmanPairsEngineSample()
    {
        // Симулируем цены: в середине массив содержит аномальное расширение спреда
        double[] assetX = { 100, 101, 100, 102, 101, 102, 103, 104, 103, 102 };
        double[] assetY = { 150, 152, 149, 153, 162, 164, 155, 156, 154, 153 }; // На шагах 5 и 6 искусственный выброс вверх

        // Инициализируем робота
        var tradingEngine = new KalmanPairsEngine(initialBeta: 1.5);

        Console.WriteLine("Шаг\tЦена X\tЦена Y\tBeta\tСпред\tZ-Score\tСигнал");
        Console.WriteLine("----------------------------------------------------------------------");

        for (int i = 0; i < assetX.Length; i++)
        {
            var result = tradingEngine.ProcessTick(assetX[i], assetY[i]);

            Console.WriteLine($"{i + 1}\t{assetX[i]:F1}\t{assetY[i]:F1}\t{result.Beta:F3}\t{result.Spread:F2}\t{result.ZScore:F2}\t{result.Signal}");
        }
    }
}
