using Market.Core.Models;

namespace Market.Infrastructure.Tests;

public class StrategyRegistryTests
{
    [Test]
    public void Register_ThenTryGet_ReturnsSameInstance()
    {
        // Arrange
        var registry = new StrategyRegistry();
        var algo = new TestAlgo(Guid.NewGuid(), assetX: 1, assetY: 2);

        // Act
        registry.Register(algo);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(registry.TryGet(algo.StrategyId, out var found), Is.True);
            Assert.That(found, Is.SameAs(algo));
        });
    }

    [Test]
    public void TryGet_UnknownStrategy_ReturnsFalse()
    {
        var registry = new StrategyRegistry();

        var found = registry.TryGet(Guid.NewGuid(), out var algo);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.False);
            Assert.That(algo, Is.Null);
        });
    }

    [Test]
    public void Register_SameStrategyId_ReplacesInstance()
    {
        // Arrange
        var registry = new StrategyRegistry();
        var strategyId = Guid.NewGuid();
        var first = new TestAlgo(strategyId, assetX: 1, assetY: 2);
        var second = new TestAlgo(strategyId, assetX: 3, assetY: 4);
        registry.Register(first);

        // Act
        registry.Register(second);

        // Assert
        Assert.That(registry.TryGet(strategyId, out var found), Is.True);
        Assert.That(found, Is.SameAs(second));
    }

    [Test]
    public void Remove_DeletesOnlyRequestedStrategy()
    {
        // Arrange
        var registry = new StrategyRegistry();
        var removed = new TestAlgo(Guid.NewGuid(), assetX: 1, assetY: 2);
        var kept = new TestAlgo(Guid.NewGuid(), assetX: 3, assetY: 4);
        registry.Register(removed);
        registry.Register(kept);

        // Act
        var result = registry.Remove(removed.StrategyId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(registry.TryGet(removed.StrategyId, out _), Is.False);
            Assert.That(registry.TryGet(kept.StrategyId, out _), Is.True);
        });
    }

    [Test]
    public void Remove_UnknownStrategy_ReturnsFalse()
    {
        var registry = new StrategyRegistry();

        Assert.That(registry.Remove(Guid.NewGuid()), Is.False);
    }

    [Test]
    public void Register_Null_Throws()
    {
        var registry = new StrategyRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    private sealed class TestAlgo(Guid strategyId, ushort assetX, ushort assetY) : IPairTradingAlgo
    {
        public Guid StrategyId { get; } = strategyId;
        public ushort AssetX { get; } = assetX;
        public ushort AssetY { get; } = assetY;

        public PairTradingSignal? ProcessBook(OrderBookDepth book) => null;
    }
}
