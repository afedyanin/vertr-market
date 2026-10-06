using Market.Core.Algos.PairTrading;
using Market.Core.Tests.Stubs;

namespace Market.Core.Tests.Algos.PairTrading;

public class KalmanPairsAlgoTests
{
    [Test]
    public void KalmanPairsAlgoSample()
    {
        // Симулируем цены: в середине массив содержит аномальное расширение спреда
        double[] assetX = { 100, 101, 100, 102, 101, 102, 103, 104, 103, 102 };
        double[] assetY = { 150, 152, 149, 153, 162, 164, 155, 156, 154, 153 }; // На шагах 5 и 6 искусственный выброс вверх

        // Инициализируем робота
        var tradingEngine = new KalmanPairsAlgo(initialBeta: 1.5);

        Console.WriteLine("Шаг\tЦена X\tЦена Y\tBeta\tСпред\tZ-Score\tСигнал");
        Console.WriteLine("----------------------------------------------------------------------");

        var time = new DateTime(2026, 10, 06, 10, 10, 10);

        for (int i = 0; i < assetX.Length; i++)
        {
            time = time.AddMilliseconds(i);

            var bookX = OrderBookStub.Create(time, 1, assetX[i], assetX[i]);
            var bookY = OrderBookStub.Create(time, 5, assetY[i], assetY[i]);

            _ = tradingEngine.ProcessTickX(bookX);
            var resultY = tradingEngine.ProcessTickY(bookY);

            var result = resultY as KalmanPairsAlgoSignal;

            if (result != null)
            {
                Console.WriteLine($"{i + 1}\t{bookX.MidPrice:F1}\t{bookY.MidPrice:F1}\t{result.Beta:F3}\t{result.Spread:F2}\t{result.ZScore:F2}\t{result.Signal}");
            }
        }
    }
}
