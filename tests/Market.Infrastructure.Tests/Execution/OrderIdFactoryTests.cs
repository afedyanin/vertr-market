using Market.Infrastructure.Execution;

namespace Market.Infrastructure.Tests.Execution;

public class OrderIdFactoryTests
{
    private static readonly DateTime Timestamp = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Create_IsDeterministicAndFitsApiLimit()
    {
        // Arrange
        var strategyId = Guid.NewGuid();

        // Act
        var first = OrderIdFactory.Create(strategyId, Timestamp, assetId: 12, qtyLots: 3);
        var second = OrderIdFactory.Create(strategyId, Timestamp, assetId: 12, qtyLots: 3);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(second), "Одинаковый сигнал должен давать один idempotency key");
            Assert.That(first.Length, Is.LessThanOrEqualTo(OrderIdFactory.MaxLength));
            Assert.That(Guid.TryParse(first, out _), Is.True);
        });
    }

    [Test]
    public void Create_DifferentLegs_ProduceDifferentIds()
    {
        // Arrange
        var strategyId = Guid.NewGuid();

        // Act
        var xLeg = OrderIdFactory.Create(strategyId, Timestamp, assetId: 12, qtyLots: 3);
        var yLeg = OrderIdFactory.Create(strategyId, Timestamp, assetId: 13, qtyLots: -3);

        // Assert
        Assert.That(xLeg, Is.Not.EqualTo(yLeg));
    }

    [Test]
    public void CreateCompensation_DoesNotCollideWithOriginalOrder()
    {
        // Arrange
        var original = OrderIdFactory.Create(Guid.NewGuid(), Timestamp, assetId: 12, qtyLots: 3);

        // Act
        var compensation = OrderIdFactory.CreateCompensation(original);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(compensation, Is.Not.EqualTo(original));
            Assert.That(compensation.Length, Is.LessThanOrEqualTo(OrderIdFactory.MaxLength));
            Assert.That(Guid.TryParse(compensation, out _), Is.True);
        });
    }

    [Test]
    public void CreateCompensation_EmptyOrderId_Throws()
    {
        Assert.Throws<ArgumentException>(() => OrderIdFactory.CreateCompensation(" "));
    }
}
