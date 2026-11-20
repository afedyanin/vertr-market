using Market.Infrastructure.Execution;

namespace Market.Infrastructure.Tests.Execution;

public class OrderCommissionTests
{
    [Test]
    public void TryTake_ReturnsAmountExactlyOnce()
    {
        var commission = new OrderCommission(1.25m);

        var first = commission.TryTake(out var firstValue);
        var second = commission.TryTake(out var secondValue);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(firstValue, Is.EqualTo(1.25m));
            Assert.That(second, Is.False);
            Assert.That(secondValue, Is.Zero);
        });
    }

    [Test]
    public void TryTake_ZeroAmount_ReturnsFalse()
    {
        var commission = new OrderCommission(0m);

        Assert.Multiple(() =>
        {
            Assert.That(commission.TryTake(out _), Is.False);
            Assert.That(commission.Amount, Is.Zero);
        });
    }

    [Test]
    public void Constructor_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new OrderCommission(-0.01m));
    }

    [Test]
    public async Task TryTake_FromManyThreads_ReturnsAmountOnlyOnce()
    {
        // Arrange
        const int attempts = 1_000;
        var commission = new OrderCommission(10m);
        var gate = new object();
        var taken = 0;
        var total = 0m;

        // Act
        await Task.WhenAll(Enumerable.Range(0, attempts).Select(_ => Task.Run(() =>
        {
            if (!commission.TryTake(out var value))
            {
                return;
            }

            Interlocked.Increment(ref taken);

            lock (gate)
            {
                total += value;
            }
        })));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(taken, Is.EqualTo(1));
            Assert.That(total, Is.EqualTo(10m));
        });
    }
}
