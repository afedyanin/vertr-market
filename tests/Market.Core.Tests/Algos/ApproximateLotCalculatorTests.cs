using Market.Core.Algos.Common;

namespace Market.Core.Tests.Algos;


[TestFixture]
public class ApproximateLotCalculatorTests
{
    [TestCase(0.73, 100, 10)]
    [TestCase(0.73, 10, 100)]
    public void CalculateBestApproximation_MySample(double beta, int lotSizeX, int lotSizeY)
    {
        (var lotsX, var lotsY) = ApproximateLotCalculator.CalculateLots(beta, lotSizeX, lotSizeY, maxLots: 20);
        Console.WriteLine($"LotsX={lotsX} LotsY={lotsY}");
    }

    [TestCase(0.73)]
    [TestCase(1.3)]
    [TestCase(0.095)]
    [TestCase(0.325)]
    [TestCase(0.1)]
    [TestCase(2.1)]
    [TestCase(1.47)]
    public void CalculateBestApproximation_MySampleBeta(double beta)
    {
        (var lotsX, var lotsY) = ApproximateLotCalculator.CalculateLots(beta);
        Console.WriteLine($"Beta={beta} LotsX={lotsX} LotsY={lotsY}");
    }

    [Test]
    public void CalculateBestApproximation_ExactRatio_ReturnsMinimalExactLots()
    {
        // Act
        var result = ApproximateLotCalculator.CalculateLots(0.5, 10, 1);

        Console.WriteLine($"LotsX={result.lotsX} LotsY={result.lotsY}");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.lotsX, Is.EqualTo(1));
            Assert.That(result.lotsY, Is.EqualTo(5));
        });
    }

    [Test]
    public void CalculateBestApproximation_Case1_LotSizeYGreaterThanLotSizeX_Returns9To1()
    {
        // Act
        var result = ApproximateLotCalculator.CalculateLots(1.13, 10, 100, 10);
        Console.WriteLine($"LotsX={result.lotsX} LotsY={result.lotsY}");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.lotsX, Is.EqualTo(9));
            Assert.That(result.lotsY, Is.EqualTo(1));
        });
    }

    [Test]
    public void CalculateBestApproximation_Case2_LotSizeXGreaterThanLotSizeY_Returns1To11()
    {
        // Act
        var result = ApproximateLotCalculator.CalculateLots(1.13, 100, 10, 20);
        Console.WriteLine($"LotsX={result.lotsX} LotsY={result.lotsY}");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.lotsX, Is.EqualTo(1));
            Assert.That(result.lotsY, Is.EqualTo(11));
        });
    }

    [Test]
    public void CalculateBestApproximation_IrrationalRatioPi_FindsClassic22To7Approximation()
    {
        // Act
        // maxLots должен быть не меньше 22, иначе ограничение x <= maxLots
        // отсекает классическое приближение 22/7.
        var result = ApproximateLotCalculator.CalculateLots(Math.PI, 1, 1, maxLots: 25);
        Console.WriteLine($"LotsX={result.lotsX} LotsY={result.lotsY}");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.lotsX, Is.EqualTo(7));
            Assert.That(result.lotsY, Is.EqualTo(22));
        });
    }

    [Test]
    public void CalculateBestApproximation_HigherMaxLotsLimit_FindsMoreAccurateApproximation()
    {
        // Arrange: для числа Пи при лимите 500 метод должен найти знаменитую дробь 355 / 113

        // Act
        var result = ApproximateLotCalculator.CalculateLots(Math.PI, 1, 1, maxLots: 500);
        Console.WriteLine($"LotsX={result.lotsX} LotsY={result.lotsY}");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.lotsX, Is.EqualTo(113));
            Assert.That(result.lotsY, Is.EqualTo(355));
        });
    }
}

