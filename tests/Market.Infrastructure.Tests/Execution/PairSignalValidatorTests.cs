using Market.Infrastructure.Execution;

namespace Market.Infrastructure.Tests.Execution;

public class PairSignalValidatorTests
{
    [TestCase(3, -3)]
    [TestCase(-3, 3)]
    [TestCase(1, -10)]
    public void TryValidate_OppositeNonZeroQuantities_IsValid(int xQtyLots, int yQtyLots)
    {
        var isValid = PairSignalValidator.TryValidate(xQtyLots, yQtyLots, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(error, Is.Empty);
        });
    }

    [TestCase(0, -3)]
    [TestCase(3, 0)]
    [TestCase(0, 0)]
    public void TryValidate_ZeroQuantity_IsInvalid(int xQtyLots, int yQtyLots)
    {
        var isValid = PairSignalValidator.TryValidate(xQtyLots, yQtyLots, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(error, Is.Not.Empty);
        });
    }

    [TestCase(3, 3)]
    [TestCase(-3, -3)]
    public void TryValidate_SameSignQuantities_IsInvalid(int xQtyLots, int yQtyLots)
    {
        Assert.That(PairSignalValidator.TryValidate(xQtyLots, yQtyLots, out _), Is.False);
    }

    [Test]
    public void TryValidate_MinValue_IsInvalid()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PairSignalValidator.TryValidate(int.MinValue, 1, out _), Is.False);
            Assert.That(PairSignalValidator.TryValidate(1, int.MinValue, out _), Is.False);
        });
    }
}
