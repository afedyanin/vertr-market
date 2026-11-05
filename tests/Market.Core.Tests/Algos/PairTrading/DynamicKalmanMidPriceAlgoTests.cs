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

            var meta = new AlgoMetadata
            {
                AssetX = 1,
                AssetY = 5,
                StrategyId = Guid.NewGuid(),
                AlgoType = nameof(DynamicKalmanMidPriceAlgo),
            };

            meta.AlgoParams.TryAdd(AlgoMetadata.Keys.LotSizeX, 1);
            meta.AlgoParams.TryAdd(AlgoMetadata.Keys.LotSizeY, 100);
            meta.AlgoParams.TryAdd(AlgoMetadata.Keys.InitialBeta, 1.0);
            meta.AlgoParams.TryAdd(AlgoMetadata.Keys.InitialErrorCovariance, 1.0);
            meta.AlgoParams.TryAdd(AlgoMetadata.Keys.Q, 0.00005);
            meta.AlgoParams.TryAdd(AlgoMetadata.Keys.R, 0.01);

            var algo = new DynamicKalmanMidPriceAlgo(meta);

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
