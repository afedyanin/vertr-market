using Market.Core.Algos.PairTrading;
using Market.Core.FileStore;
using Market.Core.Models;
using Market.Infrastructure;
using Market.Infrastructure.Repositories;

namespace Market.Core.Tests.Algos.PairTrading;

public class KalmanMidPriceAlgoTestsBack
{
    [TestCase("Data\\books_2026-10-05.bin.gz")]
    [TestCase("Data\\books_2026-10-06.bin.gz")]
    public async Task BackTestSimple(string filePath)
    {
        using var cts = new CancellationTokenSource();

        try
        {
            var orderExec = new BacktestOrderExecution();
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

                Console.WriteLine($"Signal: XQty={signal.XQtyLots} YQty={signal.YQtyLots} XBid={signal.BookX.BestBid} XAsk={signal.BookX.BestAsk} YBid={signal.BookY.BestBid} YAsk={signal.BookY.BestAsk}");

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
