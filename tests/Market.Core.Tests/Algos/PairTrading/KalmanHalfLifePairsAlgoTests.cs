using Market.Core.Algos.PairTrading;
using Market.Core.Tests.Stubs;

namespace Market.Core.Tests.Algos.PairTrading;

public class KalmanHalfLifePairsAlgoTests
{
    [Test]
    public void KalmanHalfLifePairsAlgoSample()
    {
        // Имитируем 10 секунд торгов. На 6-й секунде коинтеграция рушится (спред улетает и не возвращается)
        double[] assetX = { 100.0, 100.2, 100.1, 100.3, 100.4, 100.5, 100.6, 100.7, 100.8, 100.9 };
        double[] assetY = { 150.0, 150.1, 150.3, 150.2, 150.8, 153.0, 156.0, 159.0, 162.0, 165.0 };

        // Инициализируем робота
        var algo = new KalmanHalfLifePairsAlgo(1, 5, initialBeta: 1.5);

        Console.WriteLine("Sec\tSpread\tZ-ScoreBuy\tZ-ScoreSell\tIsCointegrated\tKappa (Speed)\tHalf-Life(sec)\tSignal");
        Console.WriteLine("-------------------------------------------------------------------------------------------");

        var time = new DateTime(2026, 10, 06, 10, 10, 10);

        for (int i = 0; i < assetX.Length; i++)
        {
            time = time.AddMilliseconds(i);

            var bookX = OrderBookStub.Create(time, algo.AssetX, assetX[i], assetX[i]);
            var bookY = OrderBookStub.Create(time, algo.AssetY, assetY[i], assetY[i]);

            _ = algo.ProcessTick(bookX);
            var resultY = algo.ProcessTick(bookY);

            var result = resultY as KalmanHalfLifePairsSignal;

            if (result != null)
            {
                string halfLifeStr = result.IsCointegrated ? $"{result.HalfLifeSeconds:F1}s" : "INF ";
                Console.WriteLine($"{i + 1}\t{result.Spread:F2}\t{result.ZScoreBuy:F2}\t\t{result.ZScoreSell:F2}\t\t{result.IsCointegrated}\t\t\t{result.Kappa:F4}\t\t\t{halfLifeStr}\t\t\t{result.Signal}");
            }
        }
    }
}
