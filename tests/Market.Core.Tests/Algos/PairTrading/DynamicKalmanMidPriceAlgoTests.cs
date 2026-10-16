using Market.Core.Algos.PairTrading;
using Market.Core.Extensions;
using Market.Core.FileStore;
using Market.Core.Models;

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
            const string rowTemplate = "{0,-6} {1, 12} {2,12} {3,12} {4,12} {5,12} {6,12} {7, 12}";
            Console.WriteLine(rowTemplate, "Шаг", "PriceX", "PriceY", "Beta", "Spread", "Z-Score", "XQty", "YQty");
            Console.WriteLine(new string('-', 85));

            var algo = new KalmanMidPriceAlgo(
                1, 5, 1, 100,
                initialBeta: 1.0,
                initialErrorCovariance: 1.0,
                q: 0.00005,
                r: 0.01);

            var i = 0;

            await foreach (var book in OrderBookFileReader.ReadAsync(filePath, cts.Token))
            {
                var signal = algo.ProcessBook(book) as PairTradingSignal;

                if (signal != null)
                {
                    Console.WriteLine(rowTemplate,
                        i + 1,
                        signal.GetParam(SignalParamKeys.PriceX)?.ToString("F5"),
                        signal.GetParam(SignalParamKeys.PriceY)?.ToString("F5"),
                        signal.GetParam(SignalParamKeys.Beta)?.ToString("F5"),
                        signal.GetParam(SignalParamKeys.Spread)?.ToString("F5"),
                        signal.GetParam(SignalParamKeys.ZScore)?.ToString("F5"),
                        signal.XQty,
                        signal.YQty);
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
