using Market.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Market.Infrastructure.Tests;

public class PositionReconcilerTests
{
    private const ushort AssetX = 1;
    private const ushort AssetY = 2;

    [Test]
    public void TryReconcile_AppliesAbsolutePortfolioQuantitiesInLots()
    {
        // Arrange
        var strategyId = Guid.NewGuid();
        var algo = new ReconcilableAlgo(strategyId);
        var reconciler = CreateReconciler(algo);

        var portfolio = new Portfolio { StrategyId = strategyId };
        portfolio.Update(Trade(AssetX, qty: 2m, price: 100m));
        portfolio.Update(Trade(AssetX, qty: 1m, price: 110m));
        portfolio.Update(Trade(AssetY, qty: -3m, price: 200m));

        // Act
        var reconciled = reconciler.TryReconcile(portfolio);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(reconciled, Is.True);
            Assert.That(algo.CallCount, Is.EqualTo(1));
            Assert.That(algo.LastXQtyLots, Is.EqualTo(3));
            Assert.That(algo.LastYQtyLots, Is.EqualTo(-3));
        });
    }

    [Test]
    public void TryReconcile_EmptyPortfolio_ResetsPositionToZero()
    {
        // Arrange
        var strategyId = Guid.NewGuid();
        var algo = new ReconcilableAlgo(strategyId);
        var reconciler = CreateReconciler(algo);

        // Act
        var reconciled = reconciler.TryReconcile(new Portfolio { StrategyId = strategyId });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(reconciled, Is.True);
            Assert.That(algo.LastXQtyLots, Is.Zero);
            Assert.That(algo.LastYQtyLots, Is.Zero);
        });
    }

    [Test]
    public void TryReconcile_UnknownStrategy_DoesNothing()
    {
        // Arrange
        var other = new ReconcilableAlgo(Guid.NewGuid());
        var reconciler = CreateReconciler(other);

        // Act
        var reconciled = reconciler.TryReconcile(new Portfolio { StrategyId = Guid.NewGuid() });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(reconciled, Is.False);
            Assert.That(other.CallCount, Is.Zero);
        });
    }

    [Test]
    public void TryReconcile_AlgoWithoutReconciliationSupport_ReturnsFalse()
    {
        // Arrange
        var algo = new PlainAlgo(Guid.NewGuid());
        var registry = new StrategyRegistry();
        registry.Register(algo);
        var reconciler = new PositionReconciler(registry, NullLogger<PositionReconciler>.Instance);

        // Act
        var reconciled = reconciler.TryReconcile(new Portfolio { StrategyId = algo.StrategyId });

        // Assert
        Assert.That(reconciled, Is.False);
    }

    [Test]
    public void TryReconcile_NullPortfolio_Throws()
    {
        var reconciler = CreateReconciler(new ReconcilableAlgo(Guid.NewGuid()));

        Assert.Throws<ArgumentNullException>(() => reconciler.TryReconcile(null!));
    }

    private static PositionReconciler CreateReconciler(IPairTradingAlgo algo)
    {
        var registry = new StrategyRegistry();
        registry.Register(algo);
        return new PositionReconciler(registry, NullLogger<PositionReconciler>.Instance);
    }

    private static MyTrade Trade(ushort assetId, decimal qty, decimal price) => new()
    {
        AssetId = assetId,
        Qty = qty,
        Price = price,
        Timestamp = DateTime.UtcNow,
        TradeId = Guid.NewGuid().ToString("N"),
    };

    private sealed class ReconcilableAlgo(Guid strategyId) : IPositionReconcilable
    {
        public Guid StrategyId { get; } = strategyId;
        public ushort AssetX => PositionReconcilerTests.AssetX;
        public ushort AssetY => PositionReconcilerTests.AssetY;

        public int CallCount { get; private set; }
        public int LastXQtyLots { get; private set; }
        public int LastYQtyLots { get; private set; }

        public PairTradingSignal? ProcessBook(OrderBookDepth book) => null;

        public void ReconcilePosition(int xQtyLots, int yQtyLots)
        {
            CallCount++;
            LastXQtyLots = xQtyLots;
            LastYQtyLots = yQtyLots;
        }
    }

    private sealed class PlainAlgo(Guid strategyId) : IPairTradingAlgo
    {
        public Guid StrategyId { get; } = strategyId;
        public ushort AssetX => PositionReconcilerTests.AssetX;
        public ushort AssetY => PositionReconcilerTests.AssetY;

        public PairTradingSignal? ProcessBook(OrderBookDepth book) => null;
    }
}
