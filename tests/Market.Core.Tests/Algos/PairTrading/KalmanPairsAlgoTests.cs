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

        Console.WriteLine("Шаг\tЦена X\tЦена Y\tBeta\tСпред\tZ-Score\tСигнал");
        Console.WriteLine("----------------------------------------------------------------------");

        var time = new DateTime(2026, 10, 06, 10, 10, 10);

        for (int i = 0; i < assetX.Length; i++)
        {
            time = time.AddMilliseconds(i);

            var bookX = OrderBookStub.Create(time, algo.AssetX, assetX[i], assetX[i]);
            var bookY = OrderBookStub.Create(time, algo.AssetY, assetY[i], assetY[i]);

            _ = algo.ProcessTick(bookX);
            var resultY = algo.ProcessTick(bookY);

            var result = resultY as KalmanPairsAlgoSignal;

            if (result != null)
            {
                Console.WriteLine($"{i + 1}\t{bookX.MidPrice:F1}\t{bookY.MidPrice:F1}\t{result.Beta:F3}\t{result.Spread:F2}\t{result.ZScore:F2}\t{result.Signal}");
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
            const string rowTemplate = "{0,-6} {1,12} {2,12} {3,12} {4,10} {5,10} {6,-8}";
            Console.WriteLine(rowTemplate, "Шаг", "Цена X", "Цена Y", "Beta", "Спред", "Z-Score", "Сигнал");
            Console.WriteLine(new string('-', 85));

            var algo = new KalmanPairsAlgo(1, 6, initialBeta: 100);
            //var algo = new KalmanPairsAlgo(5, 6);
            var i = 0;

            await foreach (var book in OrderBookFileReader.ReadAsync(filePath, cts.Token))
            {
                var signal = algo.ProcessTick(book) as KalmanPairsAlgoSignal;

                if (signal != null && signal.Signal != "HOLD")
                {
                    Console.WriteLine(rowTemplate,
                        i + 1,
                        signal.PriceX.ToString("F4"),
                        signal.PriceY.ToString("F4"),
                        signal.Beta.ToString("F3"),
                        signal.Spread.ToString("F2"),
                        signal.ZScore.ToString("F2"),
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
