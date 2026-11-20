using Market.Core.Algos.PairTradingV2;
using Market.Core.Models;
using Market.Infrastructure;

namespace Market.BacktestEngine.Tests;

public class BacktestRunnerTests
{
    private const ushort AssetX = 1;
    private const ushort AssetY = 5;
    private static readonly DateTime StartTime = new(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task RunAsync_SyntheticPair_ClosesOpenPositionAtEnd()
    {
        // Arrange: плоский рынок, затем выброс по Y. После выброса данных нет,
        // поэтому позиция должна быть закрыта последним пригодным стаканом.
        var strategy = CreateStrategy();
        var books = new List<OrderBookDepth>();

        for (var i = 0; i < 200; i++)
        {
            var timestamp = StartTime.AddSeconds(i);
            books.Add(Book(timestamp, AssetX, bid: 99, ask: 101));
            books.Add(Book(timestamp, AssetY, bid: 149, ask: 151));
        }

        var spikeTime = StartTime.AddSeconds(200);
        books.Add(Book(spikeTime, AssetX, bid: 99, ask: 101));
        books.Add(Book(spikeTime, AssetY, bid: 155, ask: 157));

        var emptyTime = spikeTime.AddMilliseconds(1);
        books.Add(new OrderBookDepth(emptyTime, AssetX, [], []));
        books.Add(new OrderBookDepth(emptyTime, AssetY, [], []));

        var request = new BacktestRequest(
            strategy,
            AsAsync(books),
            new BacktestExecutionOptions
            {
                SlippagePercent = 1.0,
                CommissionPercent = 0.1,
            });

        // Act
        var result = await BacktestRunner.RunAsync(request);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ProcessedBooks, Is.EqualTo(books.Count));
            Assert.That(result.SignalCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(result.LiquidationTradeCount, Is.EqualTo(2));
            Assert.That(result.Portfolio.Commission, Is.GreaterThan(0m));
            Assert.That(strategy.HasOpenPosition, Is.False);

            Assert.That(
                result.Portfolio.Positions.Select(position => position.Quantity),
                Is.All.Zero);
        });

        // X закрывался продажей по bid 99 с проскальзыванием 1%,
        // Y — покупкой по ask 157 с проскальзыванием 1%.
        var liquidation = result.Portfolio.Trades
            .Skip(result.Portfolio.Trades.Length - result.LiquidationTradeCount)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(liquidation.Single(t => t.AssetId == AssetX).Price, Is.EqualTo(99m * 0.99m));
            Assert.That(liquidation.Single(t => t.AssetId == AssetY).Price, Is.EqualTo(157m * 1.01m));
        });
    }

    [Test]
    public async Task RunAsync_FiltersBooksByDateRange()
    {
        // Arrange
        var books = new List<OrderBookDepth>
        {
            Book(StartTime, AssetX, bid: 99, ask: 101),
            Book(StartTime.AddHours(1), AssetX, bid: 99, ask: 101),
            Book(StartTime.AddHours(2), AssetX, bid: 99, ask: 101),
        };

        var algo = new NullAlgo(Guid.NewGuid());

        // Act: границы включительно.
        var result = await BacktestRunner.RunAsync(
            new BacktestRequest(
                algo,
                AsAsync(books),
                new BacktestExecutionOptions(),
                StartTime.AddHours(1),
                StartTime.AddHours(2)));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ProcessedBooks, Is.EqualTo(2));
            Assert.That(result.SignalCount, Is.Zero);
            Assert.That(result.Portfolio.Trades, Is.Empty);
        });
    }

    [Test]
    public async Task RunAsync_SinglePointDateRange_IncludesBoundaryBook()
    {
        var books = new List<OrderBookDepth>
        {
            Book(StartTime, AssetX, bid: 99, ask: 101),
            Book(StartTime.AddMinutes(1), AssetX, bid: 99, ask: 101),
        };

        var result = await BacktestRunner.RunAsync(
            new BacktestRequest(
                new NullAlgo(Guid.NewGuid()),
                AsAsync(books),
                new BacktestExecutionOptions(),
                StartTime,
                StartTime));

        Assert.That(result.ProcessedBooks, Is.EqualTo(1));
    }

    [Test]
    public async Task RunAsync_WithoutSignals_ReturnsEmptyPortfolio()
    {
        var books = new List<OrderBookDepth>
        {
            Book(StartTime, AssetX, bid: 99, ask: 101),
        };

        var result = await BacktestRunner.RunAsync(
            new BacktestRequest(
                new NullAlgo(Guid.NewGuid()),
                AsAsync(books),
                new BacktestExecutionOptions()));

        Assert.Multiple(() =>
        {
            Assert.That(result.ProcessedBooks, Is.EqualTo(1));
            Assert.That(result.SignalCount, Is.Zero);
            Assert.That(result.LiquidationTradeCount, Is.Zero);
            Assert.That(result.Portfolio.Trades, Is.Empty);
            Assert.That(result.Portfolio.Positions, Is.Empty);
        });
    }

    private static KalmanFilterBasicStrategy CreateStrategy()
        => new(new AlgoMetadata
        {
            StrategyId = Guid.NewGuid(),
            AssetX = AssetX,
            AssetY = AssetY,
            AlgoType = nameof(KalmanFilterBasicStrategy),
            AlgoParams = new Dictionary<string, double>
            {
                [AlgoMetadata.Keys.EntryThreshold] = 1.0,
                [AlgoMetadata.Keys.ExitThreshold] = 0.3,
                [AlgoMetadata.Keys.WarmupTicks] = 0,
            },
        });

    private static OrderBookDepth Book(DateTime timestamp, ushort assetId, double bid, double ask)
        => new(
            timestamp,
            assetId,
            [new OrderBookLevel(bid, 100)],
            [new OrderBookLevel(ask, 100)]);

    private static async IAsyncEnumerable<OrderBookDepth> AsAsync(IEnumerable<OrderBookDepth> books)
    {
        foreach (var book in books)
        {
            yield return book;
        }

        await Task.CompletedTask;
    }

    private sealed class NullAlgo(Guid strategyId) : IPairTradingAlgo
    {
        public Guid StrategyId { get; } = strategyId;
        public ushort AssetX => BacktestRunnerTests.AssetX;
        public ushort AssetY => BacktestRunnerTests.AssetY;

        public PairTradingSignal? ProcessBook(OrderBookDepth book) => null;
    }
}
