using Market.Core.Algos.PairTrading;

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
        var kalman = new KalmanFilterEstimator(initialState: 1.0, initialErrorCovariance: 1.0);

        Console.WriteLine("Шаг\tЦена X\tЦена Y\t Beta\t Spread");
        Console.WriteLine("-----------------------------------");

        for (int i = 0; i < assetX.Length; i++)
        {
            double x = assetX[i];
            double y = assetY[i];

            // Обновляем фильтр текущими ценами и получаем новую бету
            double currentBeta = kalman.Update(x, y);

            // Расчет текущего спреда: Y - Beta * X
            double spread = y - (currentBeta * x);

            Console.WriteLine($"{i + 1}\t{x:F1}\t{y:F1}\t{currentBeta:F4}\t{spread:F4}");
        }
    }
}
