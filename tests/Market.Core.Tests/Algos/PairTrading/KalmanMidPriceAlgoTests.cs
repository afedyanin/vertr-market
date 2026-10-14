using Market.Core.Algos.PairTrading;
using Market.Core.Extensions;
using Market.Core.FileStore;
using Market.Core.Models;
using Market.Core.Tests.Stubs;

namespace Market.Core.Tests.Algos.PairTrading;

public class KalmanMidPriceAlgoTests
{
    [Test]
    public void KalmanPairsAlgoSample()
    {
        // Симулируем цены: в середине массив содержит аномальное расширение спреда
        double[] assetX = { 100, 101, 100, 102, 101, 102, 103, 104, 103, 102 };
        double[] assetY = { 150, 152, 149, 153, 162, 164, 155, 156, 154, 153 }; // На шагах 5 и 6 искусственный выброс вверх

        // Инициализируем робота
        var algo = new KalmanMidPriceAlgo(1, 5, initialBeta: 1.5);

        const string rowTemplate = "{0,-6} {1, 12} {2,10} {3,-8}";
        Console.WriteLine(rowTemplate, "Шаг", "Beta", "Z-Score", "Сигнал");
        Console.WriteLine(new string('-', 50));

        var time = new DateTime(2026, 10, 06, 10, 10, 10);

        for (int i = 0; i < assetX.Length; i++)
        {
            time = time.AddMilliseconds(i);

            var bookX = OrderBookStub.Create(time, algo.AssetX, assetX[i], assetX[i]);
            var bookY = OrderBookStub.Create(time, algo.AssetY, assetY[i], assetY[i]);

            _ = algo.ProcessBook(bookX);
            var signal = algo.ProcessBook(bookY);

            if (signal != null)
            {
                Console.WriteLine(rowTemplate,
                    i + 1,
                    signal.Action,
                    signal.GetParam(SignalParamKeys.Beta)?.ToString("F3"),
                    signal.GetParam(SignalParamKeys.ZScore)?.ToString("F2"),
                    signal.Action);
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

            var algo = new KalmanMidPriceAlgo(1, 5, initialBeta: 100);
            //var algo = new KalmanPairsAlgo(5, 6);
            var i = 0;

            await foreach (var book in OrderBookFileReader.ReadAsync(filePath, cts.Token))
            {
                var signal = algo.ProcessBook(book);

                if (signal != null && signal.Action != PairTradingAction.Hold)
                {
                    Console.WriteLine(rowTemplate,
                        i + 1,
                        signal.GetParam(SignalParamKeys.PriceX)?.ToString("F5"),
                        signal.GetParam(SignalParamKeys.PriceY)?.ToString("F5"),
                        signal.GetParam(SignalParamKeys.Beta)?.ToString("F5"),
                        signal.GetParam(SignalParamKeys.Spread)?.ToString("F5"),
                        signal.GetParam(SignalParamKeys.ZScore)?.ToString("F5"),
                        signal.Action);
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
