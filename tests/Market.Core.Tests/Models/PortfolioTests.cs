using Market.Core.Models;

namespace Market.Core.Tests.Models;

public class PortfolioTests
{
    [Test]
    public void Update_AppliesCommissionToPositionAndPortfolio()
    {
        // Arrange
        var portfolio = new Portfolio { StrategyId = Guid.NewGuid() };

        // Act
        portfolio.Update(Trade(assetId: 1, qty: 2m, price: 100m, commission: 0.2m));
        portfolio.Update(Trade(assetId: 1, qty: -1m, price: 110m, commission: 0.11m));

        // Assert
        var position = portfolio.Get(1);

        Assert.Multiple(() =>
        {
            Assert.That(position, Is.Not.Null);
            Assert.That(portfolio.Commission, Is.EqualTo(0.31m));
            Assert.That(position!.Commission, Is.EqualTo(0.31m));
            Assert.That(position.Cash, Is.EqualTo(-90.31m));
            Assert.That(position.Quantity, Is.EqualTo(1m));
        });
    }

    [Test]
    public void Update_CommissionIsSummedAcrossPositions()
    {
        // Arrange
        var portfolio = new Portfolio { StrategyId = Guid.NewGuid() };

        // Act
        portfolio.Update(Trade(assetId: 1, qty: 1m, price: 100m, commission: 0.1m));
        portfolio.Update(Trade(assetId: 2, qty: -1m, price: 200m, commission: 0.2m));

        // Assert
        Assert.That(portfolio.Commission, Is.EqualTo(0.3m));
        Assert.That(portfolio.Get(1)!.Commission, Is.EqualTo(0.1m));
        Assert.That(portfolio.Get(2)!.Commission, Is.EqualTo(0.2m));
    }

    private static MyTrade Trade(ushort assetId, decimal qty, decimal price, decimal commission)
        => new()
        {
            AssetId = assetId,
            Qty = qty,
            Price = price,
            Commission = commission,
            Timestamp = DateTime.UtcNow,
        };
}
