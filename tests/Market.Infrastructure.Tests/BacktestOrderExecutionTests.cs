using Market.Core.Models;

namespace Market.Infrastructure.Tests;

public class BacktestOrderExecutionTests
{
    private const ushort AssetX = 1;
    private const ushort AssetY = 2;

    [Test]
    public void Execute_BuyWalksAskLevels()
    {
        // Arrange
        var execution = new BacktestOrderExecution();
        var signal = CreateSignal(
            xQtyLots: 3,
            yQtyLots: 0,
            bookX: Book(AssetX, bids: [(99, 10)], asks: [(100, 1), (101, 2)]));

        // Act
        var trades = execution.Execute(signal);

        // Assert: один трейд на каждый уровень стакана.
        Assert.That(
            trades.Select(t => (t.Qty, t.Price)),
            Is.EqualTo(new[] { (1m, 100m), (2m, 101m) }));
    }

    [Test]
    public void Execute_SellWalksBidLevels()
    {
        // Arrange
        var execution = new BacktestOrderExecution();
        var signal = CreateSignal(
            xQtyLots: 0,
            yQtyLots: -2,
            bookY: Book(AssetY, bids: [(99, 1), (98, 1)], asks: [(100, 10)]));

        // Act
        var trades = execution.Execute(signal);

        // Assert
        Assert.That(
            trades.Select(t => (t.Qty, t.Price)),
            Is.EqualTo(new[] { (-1m, 99m), (-1m, 98m) }));
    }

    [Test]
    public void Execute_SlippageWorsensBuyAndSellPrices()
    {
        // Arrange
        var execution = new BacktestOrderExecution(slippagePercent: 1.0, commissionPercent: 0);
        var signal = CreateSignal(
            xQtyLots: 1,
            yQtyLots: -1,
            bookX: Book(AssetX, bids: [(99, 10)], asks: [(100, 10)]),
            bookY: Book(AssetY, bids: [(200, 10)], asks: [(201, 10)]));

        // Act
        var trades = execution.Execute(signal);

        // Assert: покупка дороже на 1%, продажа дешевле на 1%.
        Assert.That(
            trades.Select(t => (t.AssetId, t.Qty, t.Price)),
            Is.EqualTo(new[]
            {
                (AssetX, 1m, 101m),
                (AssetY, -1m, 198m),
            }));
    }

    [Test]
    public void Execute_CommissionIsReportedAsSeparateField()
    {
        // Arrange
        var execution = new BacktestOrderExecution(slippagePercent: 0, commissionPercent: 0.1);
        var signal = CreateSignal(
            xQtyLots: 1,
            yQtyLots: -1,
            bookX: Book(AssetX, bids: [(99, 10)], asks: [(100, 10)]),
            bookY: Book(AssetY, bids: [(100, 10)], asks: [(101, 10)]));

        // Act
        var trades = execution.Execute(signal);

        // Assert: цена не меняется, комиссия 0.1% от 100 = 0.1.
        Assert.Multiple(() =>
        {
            Assert.That(trades[0].Price, Is.EqualTo(100m));
            Assert.That(trades[0].Commission, Is.EqualTo(0.1m));
            Assert.That(trades[1].Price, Is.EqualTo(100m));
            Assert.That(trades[1].Commission, Is.EqualTo(0.1m));
        });
    }

    [Test]
    public void Execute_SlippageAndCommissionAreAppliedIndependently()
    {
        // Arrange
        var execution = new BacktestOrderExecution(slippagePercent: 1.0, commissionPercent: 0.1);
        var signal = CreateSignal(
            xQtyLots: 1,
            yQtyLots: -1,
            bookX: Book(AssetX, bids: [(99, 10)], asks: [(100, 10)]),
            bookY: Book(AssetY, bids: [(100, 10)], asks: [(101, 10)]));

        // Act
        var trades = execution.Execute(signal);

        // Assert: проскальзывание меняет цену, комиссия считается от неё.
        Assert.Multiple(() =>
        {
            Assert.That(trades[0].Price, Is.EqualTo(101m));
            Assert.That(trades[0].Commission, Is.EqualTo(101m * 0.001m));
            Assert.That(trades[1].Price, Is.EqualTo(99m));
            Assert.That(trades[1].Commission, Is.EqualTo(99m * 0.001m));
        });
    }

    [Test]
    public void Execute_WhenBookLiquidityIsInsufficient_PartiallyFillsOrder()
    {
        // Arrange
        var execution = new BacktestOrderExecution();
        var signal = CreateSignal(
            xQtyLots: 5,
            yQtyLots: 0,
            bookX: Book(AssetX, bids: [(99, 10)], asks: [(100, 2)]));

        // Act
        var trades = execution.Execute(signal);

        // Assert: исполнено только 2 лота из 5.
        Assert.Multiple(() =>
        {
            Assert.That(trades.Sum(t => t.Qty), Is.EqualTo(2m));
            Assert.That(trades, Has.Length.EqualTo(1));
            Assert.That(trades[0].Price, Is.EqualTo(100m));
        });
    }

    [Test]
    public void Execute_MaxTradeLots_SplitsFillIntoMultipleTrades()
    {
        // Arrange
        var execution = new BacktestOrderExecution(new BacktestExecutionOptions
        {
            MaxTradeLots = 1,
        });
        var signal = CreateSignal(
            xQtyLots: 4,
            yQtyLots: 0,
            bookX: Book(AssetX, bids: [(99, 10)], asks: [(100, 5)]));

        // Act
        var trades = execution.Execute(signal);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(trades, Has.Length.EqualTo(4));
            Assert.That(trades.Select(t => t.Qty), Is.All.EqualTo(1m));
            Assert.That(trades.Select(t => t.Price), Is.All.EqualTo(100m));
        });
    }

    [Test]
    public void Execute_MaxLevelParticipation_LimitsAvailableLiquidity()
    {
        // Arrange
        var execution = new BacktestOrderExecution(new BacktestExecutionOptions
        {
            MaxLevelParticipation = 0.5,
        });
        var signal = CreateSignal(
            xQtyLots: 10,
            yQtyLots: 0,
            bookX: Book(AssetX, bids: [(99, 10)], asks: [(100, 10)]));

        // Act
        var trades = execution.Execute(signal);

        // Assert: доступна половина уровня — 5 лотов.
        Assert.That(trades.Sum(t => t.Qty), Is.EqualTo(5m));
    }

    [Test]
    public void Execute_BothLegsAreFilledIndependently()
    {
        // Arrange
        var execution = new BacktestOrderExecution();
        var signal = CreateSignal(
            xQtyLots: 2,
            yQtyLots: -3,
            bookX: Book(AssetX, bids: [(99, 10)], asks: [(100, 10)]),
            bookY: Book(AssetY, bids: [(200, 10)], asks: [(201, 10)]));

        // Act
        var trades = execution.Execute(signal);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(trades.Where(t => t.AssetId == AssetX).Sum(t => t.Qty), Is.EqualTo(2m));
            Assert.That(trades.Where(t => t.AssetId == AssetY).Sum(t => t.Qty), Is.EqualTo(-3m));
        });
    }

    [Test]
    public void Execute_ZeroQuantities_ReturnNoTrades()
    {
        var execution = new BacktestOrderExecution();

        var trades = execution.Execute(CreateSignal(xQtyLots: 0, yQtyLots: 0));

        Assert.That(trades, Is.Empty);
    }

    [Test]
    public void Execute_MissingBookSide_ReturnsNoTradesForThatLeg()
    {
        // Arrange: для покупки нужны ask, но их нет.
        var execution = new BacktestOrderExecution();
        var signal = CreateSignal(
            xQtyLots: 1,
            yQtyLots: 0,
            bookX: Book(AssetX, bids: [(99, 10)], asks: []));

        // Act
        var trades = execution.Execute(signal);

        // Assert
        Assert.That(trades, Is.Empty);
    }

    [Test]
    public void Execute_SkipsInvalidPriceLevels()
    {
        // Arrange
        var execution = new BacktestOrderExecution();
        var signal = CreateSignal(
            xQtyLots: 1,
            yQtyLots: 0,
            bookX: Book(AssetX, bids: [(99, 10)], asks: [(0, 5), (100, 1)]));

        // Act
        var trades = execution.Execute(signal);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(trades, Has.Length.EqualTo(1));
            Assert.That(trades[0].Price, Is.EqualTo(100m));
        });
    }

    [Test]
    public void Execute_ProducesDeterministicAndUniqueTradeIds()
    {
        // Arrange
        var execution = new BacktestOrderExecution(new BacktestExecutionOptions
        {
            MaxTradeLots = 1,
        });
        var signal = CreateSignal(
            xQtyLots: 3,
            yQtyLots: 0,
            bookX: Book(AssetX, bids: [(99, 10)], asks: [(100, 3)]));

        // Act
        var first = execution.Execute(signal);
        var second = execution.Execute(signal);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(first.Select(t => t.TradeId), Is.Unique);
            Assert.That(first.Select(t => t.TradeId), Is.EqualTo(second.Select(t => t.TradeId)));
            Assert.That(first.Select(t => t.OrderId).Distinct(), Has.Exactly(1).Items);
        });
    }

    [TestCase(-1.0, 0.0)]
    [TestCase(0.0, -1.0)]
    [TestCase(100.0, 0.0)]
    [TestCase(0.0, 100.0)]
    [TestCase(double.NaN, 0.0)]
    public void Constructor_InvalidPercents_Throw(double slippagePercent, double commissionPercent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _ = new BacktestOrderExecution(slippagePercent, commissionPercent));
    }

    [Test]
    public void Constructor_InvalidOptions_Throw()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BacktestOrderExecution(
                new BacktestExecutionOptions { MaxTradeLots = -1 }));

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BacktestOrderExecution(
                new BacktestExecutionOptions { MaxLevelParticipation = 0 }));

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BacktestOrderExecution(
                new BacktestExecutionOptions { MaxLevelParticipation = 1.01 }));

            Assert.Throws<ArgumentNullException>(() => _ = new BacktestOrderExecution(null!));
        });
    }

    private static PairTradingSignal CreateSignal(
        int xQtyLots,
        int yQtyLots,
        OrderBookDepth? bookX = null,
        OrderBookDepth? bookY = null)
        => new()
        {
            StrategyId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            BookX = bookX ?? Book(AssetX, bids: [(99, 10)], asks: [(100, 10)]),
            BookY = bookY ?? Book(AssetY, bids: [(199, 10)], asks: [(200, 10)]),
            XQtyLots = xQtyLots,
            YQtyLots = yQtyLots,
        };

    private static OrderBookDepth Book(
        ushort assetId,
        (double Price, double Size)[] bids,
        (double Price, double Size)[] asks)
        => new(
            new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            assetId,
            [.. bids.Select(level => new OrderBookLevel(level.Price, level.Size))],
            [.. asks.Select(level => new OrderBookLevel(level.Price, level.Size))]);
}
