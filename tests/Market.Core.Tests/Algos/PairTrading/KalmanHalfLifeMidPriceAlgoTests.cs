using Market.Core.Algos.PairTrading;
using Market.Core.Extensions;
using Market.Core.FileStore;
using Market.Core.Models;
using Market.Core.Tests.Stubs;

namespace Market.Core.Tests.Algos.PairTrading;

public class KalmanHalfLifeMidPriceAlgoTests
{
    [Test]
    public void KalmanHalfLifePairsAlgoSample()
    {
        // Имитируем 10 секунд торгов. На 6-й секунде коинтеграция рушится (спред улетает и не возвращается)
        double[] assetX = { 100.0, 100.2, 100.1, 100.3, 100.4, 100.5, 100.6, 100.7, 100.8, 100.9 };
        double[] assetY = { 150.0, 150.1, 150.3, 150.2, 150.8, 153.0, 156.0, 159.0, 162.0, 165.0 };

        // Инициализируем робота
        var algo = new KalmanHalfLifeMidPriceAlgo(1, 5, initialBeta: 1.5);

        const string rowTemplate = "{0,-6} {1, 12} {2,12} {3,12} {4,12} {5,6} {6, 6} {7,-8}";
        Console.WriteLine(rowTemplate, "Шаг", "Beta", "Spread", "Z-Score Buy", "Z-Score Sell", "Half-Life(sec)", "Cointegrated", "Сигнал");
        Console.WriteLine(new string('-', 85));

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

                var isCointegrated = signal.GetParam(SignalParamKeys.IsCointegrated) > 0 ? true : false;
                var halfLifeSeconds = signal.GetParam(SignalParamKeys.HalfLifeSeconds) ?? 0;
                var halfLifeStr = isCointegrated ? $"{halfLifeSeconds:F1}s" : "INF ";

                Console.WriteLine(rowTemplate,
                    i + 1,
                    signal.GetParam(SignalParamKeys.Beta)?.ToString("F5"),
                    signal.GetParam(SignalParamKeys.Spread)?.ToString("F5"),
                    signal.GetParam(SignalParamKeys.ZScoreBuy)?.ToString("F5"),
                    signal.GetParam(SignalParamKeys.ZScoreSell)?.ToString("F5"),
                    halfLifeStr,
                    isCointegrated,
                    signal.Action.ToString());
            }
        }
    }

    [TestCase("Data\\books_2026-10-05.bin.gz")]
    [TestCase("Data\\books_2026-10-06.bin.gz")]
    public async Task KalmanHalfLifePairsAlgoBtSignals(string filePath)
    {
        using var cts = new CancellationTokenSource();

        try
        {
            const string rowTemplate = "{0,-6} {1, 12} {2,12} {3,12} {4,12} {5,6} {6, 6} {7,-8}";
            Console.WriteLine(rowTemplate, "Шаг", "Beta", "Spread", "Z-Score Buy", "Z-Score Sell", "Half-Life(sec)", "Cointegrated", "Сигнал");
            Console.WriteLine(new string('-', 85));

            var algo = new KalmanHalfLifeMidPriceAlgo(1, 5, initialBeta: 100);
            var i = 0;

            await foreach (var book in OrderBookFileReader.ReadAsync(filePath, cts.Token))
            {
                var signal = algo.ProcessBook(book);

                if (signal != null && signal.Action != PairTradingAction.Hold)
                {
                    var isCointegrated = signal.GetParam(SignalParamKeys.IsCointegrated) > 0 ? true : false;
                    var halfLifeSeconds = signal.GetParam(SignalParamKeys.HalfLifeSeconds) ?? 0;
                    var halfLifeStr = isCointegrated ? $"{halfLifeSeconds:F1}s" : "INF ";

                    Console.WriteLine(rowTemplate,
                        i + 1,
                        signal.GetParam(SignalParamKeys.Beta)?.ToString("F5"),
                        signal.GetParam(SignalParamKeys.Spread)?.ToString("F5"),
                        signal.GetParam(SignalParamKeys.ZScoreBuy)?.ToString("F5"),
                        signal.GetParam(SignalParamKeys.ZScoreSell)?.ToString("F5"),
                        halfLifeStr,
                        isCointegrated,
                        signal.Action.ToString());
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
