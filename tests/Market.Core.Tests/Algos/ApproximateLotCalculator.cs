namespace Market.Core.Tests.Algos;

public static class ApproximateLotCalculator
{

    public static (int lotsX, int lotsY) CalculateBestApproximationV2(
        double qtyX, double qtyY,
        double lotSizeX, double lotSizeY)
    {
        if (qtyX <= 0 || qtyY <= 0 || lotSizeX <= 0 || lotSizeY <= 0)
        {
            throw new InvalidOperationException("Все входные значения должны быть больше нуля.");
        }

        double ratio = (qtyY * lotSizeX) / (qtyX * lotSizeY);

        if (lotSizeX >= lotSizeY)
        {
            return (1, (int)Math.Round(ratio, MidpointRounding.AwayFromZero));
        }
        else
        {
            return ((int)Math.Round(1 / ratio, MidpointRounding.AwayFromZero), 1);
        }
    }

    public static (int lotsX, int lotsY) CalculateBestApproximation(
        double qtyX, double qtyY,
        double lotSizeX, double lotSizeY,
        int maxLots = 100)
    {
        if (qtyX <= 0 || qtyY <= 0 || lotSizeX <= 0 || lotSizeY <= 0)
        {
            throw new InvalidOperationException("Все входные значения должны быть больше нуля.");
        }

        // Целевое отношение: lotsX / lotsY
        double targetRatio = (qtyX / lotSizeX) / (qtyY / lotSizeY);

        int bestX = 1;
        int bestY = 1;
        double minError = double.MaxValue;

        // Перебираем количество лотов Y от 1 до maxLots и ищем ближайшее целое X
        for (int y = 1; y <= maxLots; y++)
        {
            int x = (int)Math.Round(y * targetRatio);
            if (x == 0)
            {
                continue;
            }

            if (x > maxLots)
            {
                break;
            }

            double currentRatio = (double)x / y;
            double relativeError = Math.Abs(currentRatio - targetRatio) / targetRatio;

            if (relativeError < minError)
            {
                minError = relativeError;
                bestX = x;
                bestY = y;

                // Если нашли идеальное совпадение, выходим досрочно
                if (minError < 1e-9)
                {
                    break;
                }
            }
        }

        return (bestX, bestY);
    }
}

[TestFixture]
public class ApproximateLotCalculatorTests
{
    [TestCase(0.73, 1, 100, 10)]
    [TestCase(0.73, 1, 10, 100)]
    public void CalculateBestApproximation_MySample(double qtyX, double qtyY, double lotSizeX, double lotSizeY)
    {
        (var lotsX, var lotsY) = ApproximateLotCalculator.CalculateBestApproximationV2(qtyX, qtyY, lotSizeX, lotSizeY);
        Console.WriteLine($"LotsX={lotsX} LotsY={lotsY}");
    }

    [Test]
    public void CalculateBestApproximation_ExactRatio_ReturnsMinimalExactLots()
    {
        // Arrange: qtyX = 15.5, lotSizeX = 10 -> 1.55 лота
        // qtyY = 3.1, lotSizeY = 1 -> 3.1 лота
        // Точное соотношение 1 к 2
        double qtyX = 15.5;
        double qtyY = 3.1;
        double lotSizeX = 10.0;
        double lotSizeY = 1.0;

        // Act
        var result = ApproximateLotCalculator.CalculateBestApproximationV2(qtyX, qtyY, lotSizeX, lotSizeY);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.lotsX, Is.EqualTo(1));
            Assert.That(result.lotsY, Is.EqualTo(2));
        });
    }

    [Test]
    public void CalculateBestApproximation_Case1_LotSizeYGreaterThanLotSizeX_Returns9To1()
    {
        // qtyY = 1, beta = 1.13 -> qtyX = 1 / 1.13 ≈ 0.88495575
        // lotSizeX = 10 -> rawLotsX ≈ 0.088495575
        // lotSizeY = 100 -> rawLotsY = 0.01
        // Отношение lotsX / lotsY ≈ 8.8495575 (ближайшее целое 9 к 1, ошибка ~1.7%)
        double qtyY = 1.0;
        double qtyX = qtyY / 1.13;
        double lotSizeX = 10.0;
        double lotSizeY = 100.0;

        // Act
        var result = ApproximateLotCalculator.CalculateBestApproximationV2(qtyX, qtyY, lotSizeX, lotSizeY);

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
        // qtyY = 1, beta = 1.13 -> qtyX ≈ 0.88495575
        // lotSizeX = 100 -> rawLotsX ≈ 0.0088495575
        // lotSizeY = 10 -> rawLotsY = 0.1
        // Отношение lotsX / lotsY ≈ 0.088495575 (обратное ≈ 11.3 -> ближайшее 1 к 11)
        double qtyY = 1.0;
        double qtyX = qtyY / 1.13;
        double lotSizeX = 100.0;
        double lotSizeY = 10.0;

        // Act
        var result = ApproximateLotCalculator.CalculateBestApproximationV2(qtyX, qtyY, lotSizeX, lotSizeY);

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
        // Arrange: классическое приближение числа Пи (3.14159265...) -> 22 / 7 ≈ 3.142857
        double qtyX = Math.PI;
        double qtyY = 1.0;
        double lotSizeX = 1.0;
        double lotSizeY = 1.0;

        // Act
        var result = ApproximateLotCalculator.CalculateBestApproximationV2(
            qtyX, qtyY, lotSizeX, lotSizeY);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.lotsX, Is.EqualTo(22));
            Assert.That(result.lotsY, Is.EqualTo(7));
        });
    }

    [Test]
    public void CalculateBestApproximation_HigherMaxLotsLimit_FindsMoreAccurateApproximation()
    {
        // Arrange: для числа Пи при лимите 500 метод должен найти знаменитую дробь 355 / 113
        double qtyX = Math.PI;
        double qtyY = 1.0;

        // Act
        var result = ApproximateLotCalculator.CalculateBestApproximation(
            qtyX, qtyY, lotSizeX: 1.0, lotSizeY: 1.0, maxLots: 500);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.lotsX, Is.EqualTo(355));
            Assert.That(result.lotsY, Is.EqualTo(113));
        });
    }

    [TestCase(0, 10, 1, 1)]
    [TestCase(-5, 10, 1, 1)]
    [TestCase(10, 0, 1, 1)]
    [TestCase(10, -5, 1, 1)]
    [TestCase(10, 10, 0, 1)]
    [TestCase(10, 10, 1, -1)]
    public void CalculateBestApproximation_NonPositiveInputs_ThrowsArgumentException(
        double qtyX, double qtyY, double lotSizeX, double lotSizeY)
    {
        Assert.Throws<ArgumentException>(() =>
            ApproximateLotCalculator.CalculateBestApproximation(qtyX, qtyY, lotSizeX, lotSizeY));
    }
}

