using Market.Core.Algos.PairTrading;
using Market.Core.FileStore;
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
        var algo = new KalmanPairsAlgo(1, 5, initialBeta: 1.5);

        const string rowTemplate = "{0,-6} {1, 12} {2,10} {3,-8}";
        Console.WriteLine(rowTemplate, "Шаг", "Beta", "Z-Score", "Сигнал");
        Console.WriteLine(new string('-', 50));

        var time = new DateTime(2026, 10, 06, 10, 10, 10);

        for (int i = 0; i < assetX.Length; i++)
        {
            time = time.AddMilliseconds(i);

            var bookX = OrderBookStub.Create(time, algo.AssetX, assetX[i], assetX[i]);
            var bookY = OrderBookStub.Create(time, algo.AssetY, assetY[i], assetY[i]);

            _ = algo.ProcessTick(bookX);
            var signal = algo.ProcessTick(bookY) as KalmanPairsAlgoSignal;

            if (signal != null)
            {
                Console.WriteLine(rowTemplate,
                    i + 1,
                        signal.Beta.ToString("F3"),
                        signal.ZScore.ToString("F2"),
                        signal.Signal);
            }
        }
    }

    [TestCase("Data\\books_2026-10-05.bin.gz")]
    [TestCase("Data\\books_2026-10-06.bin.gz")]
    public async Task KalmanPairsAlgoBtSignals(string filePath)
    {
        using var cts = new CancellationTokenSource();

        try
        {
            const string rowTemplate = "{0,-6} {1, 12} {2,12} {3,12} {4,12} {5,12} {6,-8}";
            Console.WriteLine(rowTemplate, "Шаг", "PriceX", "PriceY", "Beta", "Spread", "Z-Score", "Сигнал");
            Console.WriteLine(new string('-', 85));

            var algo = new KalmanPairsAlgo(1, 5, initialBeta: 100);
            //var algo = new KalmanPairsAlgo(5, 6);
            var i = 0;

            await foreach (var book in OrderBookFileReader.ReadAsync(filePath, cts.Token))
            {
                var signal = algo.ProcessTick(book) as KalmanPairsAlgoSignal;

                if (signal != null && signal.Signal != "HOLD")
                {
                    Console.WriteLine(rowTemplate,
                        i + 1,
                        signal.PriceX.ToString("F5"),
                        signal.PriceY.ToString("F5"),
                        signal.Beta.ToString("F5"),
                        signal.Spread.ToString("F5"),
                        signal.ZScore.ToString("F5"),
                        signal.Signal);
                }

                i++;
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Чтение было отменено.");
        }
    }
}
