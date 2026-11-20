using Market.Core.Models;
using Market.Infrastructure.Execution;

namespace Market.Infrastructure.Tests.Execution;

public class CommissionAllocatorTests
{
    [Test]
    public void Distribute_SplitsCommissionProportionallyToVolume()
    {
        // Arrange: объёмы 100 и 300, комиссия 4.
        var trades = new[]
        {
            Trade(qty: 1m, price: 100m),
            Trade(qty: 3m, price: 100m),
        };

        // Act
        var result = CommissionAllocator.Distribute(trades, 4m);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Commission, Is.EqualTo(1m));
            Assert.That(result[1].Commission, Is.EqualTo(3m));
            Assert.That(result.Sum(t => t.Commission), Is.EqualTo(4m));
        });
    }

    [Test]
    public void Distribute_PutsRoundingRemainderIntoLastTrade()
    {
        // Arrange: три равные сделки, комиссия 1 не делится на 3 нацело.
        var trades = new[]
        {
            Trade(qty: 1m, price: 10m),
            Trade(qty: 1m, price: 10m),
            Trade(qty: 1m, price: 10m),
        };

        // Act
        var result = CommissionAllocator.Distribute(trades, 1m);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Sum(t => t.Commission), Is.EqualTo(1m));
            Assert.That(result[^1].Commission, Is.EqualTo(1m - result[0].Commission - result[1].Commission));
        });
    }

    [Test]
    public void Distribute_ZeroCommission_LeavesTradesUnchanged()
    {
        var trades = new[] { Trade(qty: 2m, price: 100m) };

        var result = CommissionAllocator.Distribute(trades, 0m);

        Assert.That(result[0].Commission, Is.Zero);
    }

    [Test]
    public void Distribute_EmptyList_ReturnsEmpty()
    {
        Assert.That(CommissionAllocator.Distribute([], 1m), Is.Empty);
    }

    [Test]
    public void Distribute_ZeroTotalVolume_PutsCommissionIntoFirstTrade()
    {
        var trades = new[]
        {
            Trade(qty: 0m, price: 100m),
            Trade(qty: 0m, price: 100m),
        };

        var result = CommissionAllocator.Distribute(trades, 2m);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].Commission, Is.EqualTo(2m));
            Assert.That(result[1].Commission, Is.Zero);
        });
    }

    [Test]
    public void Distribute_NegativeCommission_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CommissionAllocator.Distribute([Trade(qty: 1m, price: 100m)], -1m));
    }

    private static MyTrade Trade(decimal qty, decimal price) => new()
    {
        AssetId = 1,
        Qty = qty,
        Price = price,
        Timestamp = DateTime.UtcNow,
    };
}
