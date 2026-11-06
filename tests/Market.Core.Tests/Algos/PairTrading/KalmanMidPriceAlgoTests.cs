using Market.Core.Algos.PairTrading;
using Market.Core.Extensions;
using Market.Core.FileStore;
using Market.Core.Models;
using Market.Core.Tests.Stubs;
using Market.Infrastructure.Repositories;

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
        var algo = CreateAlgo();

        const string rowTemplate = "{0,-6} {1, 12} {2,12} {3,12} {4,12} {5,12} {6, 12} {7, 12}";
        Console.WriteLine(rowTemplate, "Step", "PriceX", "PriceY", "Beta", "Spread", "Z-Score", "xQty", "yQty");
        Console.WriteLine(new string('-', 100));

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
                    signal.GetParam(SignalParamKeys.PriceX)?.ToString("F5"),
                    signal.GetParam(SignalParamKeys.PriceY)?.ToString("F5"),
                    signal.GetParam(SignalParamKeys.Beta)?.ToString("F5"),
                    signal.GetParam(SignalParamKeys.Spread)?.ToString("F5"),
                    signal.GetParam(SignalParamKeys.ZScore)?.ToString("F5"),
                    signal.XQty.ToString("F5"),
                    signal.YQty.ToString("F5"));
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
            const string rowTemplate = "{0,-6} {1, 12} {2,12} {3,12} {4,12} {5,12} {6, 12} {7, 12}";
            Console.WriteLine(rowTemplate, "Step", "PriceX", "PriceY", "Beta", "Spread", "Z-Score", "xQty", "yQty");
            Console.WriteLine(new string('-', 100));

            var algo = CreateAlgo();
            var i = 0;

            await foreach (var book in OrderBookFileReader.ReadAsync(filePath, cts.Token))
            {
                var signal = algo.ProcessBook(book);

                if (signal != null)
                {
                    Console.WriteLine(rowTemplate,
                        i + 1,
                        signal.GetParam(SignalParamKeys.PriceX)?.ToString("F5"),
                        signal.GetParam(SignalParamKeys.PriceY)?.ToString("F5"),
                        signal.GetParam(SignalParamKeys.Beta)?.ToString("F5"),
                        signal.GetParam(SignalParamKeys.Spread)?.ToString("F5"),
                        signal.GetParam(SignalParamKeys.ZScore)?.ToString("F5"),
                        signal.XQty.ToString("F5"),
                        signal.YQty.ToString("F5"));
                }

                i++;
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Чтение было отменено.");
        }
    }

    [TestCase("Data\\books_2026-10-05.bin.gz")]
    [TestCase("Data\\books_2026-10-06.bin.gz")]
    public async Task KalmanPairsAlgoBtTrades(string filePath)
    {
        using var cts = new CancellationTokenSource();

        try
        {
            var orderExec = new OrderExecution();
            var portfolioManager = new PortfolioRepository();
            var algo = CreateAlgo();

            var i = 0;

            await foreach (var book in OrderBookFileReader.ReadAsync(filePath, cts.Token))
            {
                var signal = algo.ProcessBook(book);

                if (signal is null)
                {
                    continue;
                }

                Console.WriteLine($"Signal: XQty={signal.XQty} YQty={signal.YQty} XBid={signal.BookX.BestBid} XAsk={signal.BookX.BestAsk} YBid={signal.BookY.BestBid} YAsk={signal.BookY.BestAsk}");

                var portfolio = portfolioManager.GetOrCreate(signal.StrategyId);
                var trades = orderExec.Execute(signal);

                foreach (var trade in trades)
                {
                    Console.WriteLine($"Trade: AssetId={trade.AssetId} Price={trade.Price} Qty={trade.Qty}");
                    portfolio.Update(trade);
                }

                i++;
            }

            foreach (var portfolio in portfolioManager.Portfolios)
            {
                Console.WriteLine($"Portfolio: {portfolio.StrategyId}");

                foreach (var pos in portfolio.Positions)
                {
                    Console.WriteLine($"-- AssetId={pos.AssetId} Qty={pos.Quantity} Price={pos.Price} Cash={pos.Cash}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Чтение было отменено.");
        }
    }

    private static KalmanMidPriceAlgo CreateAlgo()
    {
        var meta = new AlgoMetadata
        {
            AssetX = 1,
            AssetY = 5,
            StrategyId = Guid.NewGuid(),
            AlgoType = nameof(KalmanMidPriceAlgo),
        };

        meta.AlgoParams.TryAdd(AlgoMetadata.Keys.LotSizeX, 1);
        meta.AlgoParams.TryAdd(AlgoMetadata.Keys.LotSizeY, 100);
        meta.AlgoParams.TryAdd(AlgoMetadata.Keys.InitialBeta, 1.0);
        meta.AlgoParams.TryAdd(AlgoMetadata.Keys.InitialErrorCovariance, 1.0);
        meta.AlgoParams.TryAdd(AlgoMetadata.Keys.Q, 0.00005);
        meta.AlgoParams.TryAdd(AlgoMetadata.Keys.R, 0.01);

        var algo = new KalmanMidPriceAlgo(meta);

        return algo;
    }
}