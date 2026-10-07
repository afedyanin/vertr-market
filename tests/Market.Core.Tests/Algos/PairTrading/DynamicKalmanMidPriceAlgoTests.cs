using Market.Core.Algos.PairTrading;
using Market.Core.FileStore;

namespace Market.Core.Tests.Algos.PairTrading;

public class DynamicKalmanMidPriceAlgoTests
{
    [TestCase("Data\\books_2026-10-05.bin.gz")]
    [TestCase("Data\\books_2026-10-06.bin.gz")]
    public async Task DynamicKalmanMidPriceAlgoBtSignals(string filePath)
    {
        using var cts = new CancellationTokenSource();

        try
        {
            const string rowTemplate = "{0,-6} {1, 12} {2,12} {3,12} {4,12} {5,12} {6,-8}";
            Console.WriteLine(rowTemplate, "Шаг", "PriceX", "PriceY", "Beta", "Spread", "Z-Score", "Сигнал");
            Console.WriteLine(new string('-', 85));

            var algo = new KalmanMidPriceAlgo(
                1, 5,
                initialBeta: 100,
                initialErrorCovariance: 1.0,
                q: 0.00005,
                r: 0.01);

            var i = 0;

            await foreach (var book in OrderBookFileReader.ReadAsync(filePath, cts.Token))
            {
                var signal = algo.ProcessBook(book) as KalmanPairsAlgoSignal;

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
