using Market.Core.Algos.Common;

namespace Market.Core.Tests.Algos.PairTrading;

public class KalmanFilterEstimatorTests
{
    [Test]
    public void KalmanFilterEstimatorSample()
    {
        // Симулируем цены двух коинтегрированных активов (например, BTC и ETH)
        // АssetY в среднем в 1.5 раза дороже AssetX + шум
        double[] assetX = { 100, 101, 100, 102, 104, 103, 105, 106, 104, 105 };
        double[] assetY = { 150, 152, 149, 153, 155, 156, 158, 159, 155, 157 };

        // Инициализация фильтра Калмана для оценки беты
        // Начальное предположение: Beta = 1.0, Ковариация ошибки = 1.0
        var kalman = new KalmanFilterEstimator(initialBeta: 1.0, initialErrorCovariance: 1.0);

        Console.WriteLine("Шаг\tЦена X\tЦена Y\t Beta\t Spread");
        Console.WriteLine("-----------------------------------");

        for (int i = 0; i < assetX.Length; i++)
        {
            double x = assetX[i];
            double y = assetY[i];

            // Обновляем фильтр текущими ценами и получаем новую бету
            kalman.Update(x, y);

            // Расчет текущего спреда: Y - Beta * X
            double spread = y - (kalman.Beta * x);

            Console.WriteLine($"{i + 1}\t{x:F1}\t{y:F1}\t{kalman.Beta:F4}\t{spread:F4}");
        }
    }

    [TestCase(270 * 100, 28189)]
    [TestCase(28189, 270 * 100)]
    [TestCase(26189, 29900)]
    public void KalmanFilterEstimatorStep(double x, double y)
    {
        var kalman = new KalmanFilterEstimator(initialBeta: 1.0, initialErrorCovariance: 1.0);

        Console.WriteLine("Цена X\tЦена Y\t Beta\t Spread");
        Console.WriteLine("-----------------------------------");

        // Обновляем фильтр текущими ценами и получаем новую бету
        kalman.Update(x, y);

        // Расчет текущего спреда: Y - Beta * X
        double spread = y - (kalman.Beta * x);

        Console.WriteLine($"{x:F1}\t{y:F1}\t{kalman.Beta:F4}\t{spread:F8}");
    }
}
