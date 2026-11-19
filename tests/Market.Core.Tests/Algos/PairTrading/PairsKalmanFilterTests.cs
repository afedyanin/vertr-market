using Market.Core.Algos.Common;

namespace Market.Core.Tests.Algos.PairTrading;

[TestFixture]
public class PairsKalmanFilterTests
{
    [Test]
    public void CanUseKF()
    {
        var kf = new PairsKalmanFilter(delta: 1e-4, obsNoise: 1.0);

        var marketTicks = new List<(double Y, double X)>
        {
            (100.5, 50.0), (101.2, 50.3), (102.0, 50.8),
            (105.5, 51.0), (101.8, 50.9), (101.5, 50.7)
        };

        const int burnIn = 2; // В реальной торговле ставьте 30-50 баров

        for (int i = 0; i < marketTicks.Count; i++)
        {
            var (y, x) = marketTicks[i];
            var res = kf.Update(y, x);

            if (i < burnIn)
            {
                continue;
            }

            Console.WriteLine($"Tick {i} | Beta: {res.Beta:F4} | Alpha: {res.Alpha:F4} | Spread: {res.Spread:F4} | Z-score: {res.ZScore:F2}");

            if (res.ZScore > 2.0)
            {
                Console.WriteLine($"  -> SIGNAL: Short Y, Long {res.Beta:F2} X");
            }
            else if (res.ZScore < -2.0)
            {
                Console.WriteLine($"  -> SIGNAL: Long Y, Short {res.Beta:F2} X");
            }
        }
    }

    [Test]
    public void Update_StationaryCointegratedPair_ConvergesToTrueBetaAndAlpha()
    {
        // Arrange
        const double expectedBeta = 1.8;
        const double expectedAlpha = 12.5;
        const double obsNoise = 1.0;
        var data = SyntheticMarketDataGenerator.GenerateStationaryPair(
            steps: 1000,
            trueBeta: expectedBeta,
            trueAlpha: expectedAlpha,
            noiseStdDev: obsNoise);

        var filter = new PairsKalmanFilter(delta: 1e-4, obsNoise: obsNoise);
        PairsKalmanFilter.StepResult lastResult = default;

        // Act
        foreach (var tick in data)
        {
            lastResult = filter.Update(tick.Y, tick.X);
        }

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(lastResult.Beta, Is.EqualTo(expectedBeta).Within(0.1));
            Assert.That(lastResult.Alpha, Is.EqualTo(expectedAlpha).Within(5.0));
        });
    }

    [Test]
    public void Update_StationaryPair_ZScoreIsApproximatelyStandardNormal()
    {
        // Arrange
        const double obsNoise = 1.5;
        var data = SyntheticMarketDataGenerator.GenerateStationaryPair(
            steps: 2000,
            trueBeta: 1.5,
            trueAlpha: 0.0,
            noiseStdDev: obsNoise);

        var filter = new PairsKalmanFilter(delta: 1e-5, obsNoise: obsNoise);
        var zScores = new List<double>();

        // Act
        for (int i = 0; i < data.Count; i++)
        {
            var res = filter.Update(data[i].Y, data[i].X);
            if (i >= 200)
            {
                zScores.Add(res.ZScore);
            }
        }

        double meanZ = zScores.Average();
        double varianceZ = zScores.Sum(z => Math.Pow(z - meanZ, 2)) / (zScores.Count - 1);
        double stdDevZ = Math.Sqrt(varianceZ);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(meanZ, Is.EqualTo(0.0).Within(0.15));
            Assert.That(stdDevZ, Is.EqualTo(1.0).Within(0.15));
        });
    }

    [Test]
    public void Update_RegimeShift_AdaptsToNewParameters()
    {
        // Arrange
        const int totalSteps = 1500;
        const int shiftStep = 500;
        const double beta1 = 1.0, alpha1 = 10.0;
        const double beta2 = 2.2, alpha2 = -20.0;

        var data = SyntheticMarketDataGenerator.GenerateRegimeShiftPair(
            steps: totalSteps,
            shiftStep: shiftStep,
            beta1: beta1, alpha1: alpha1,
            beta2: beta2, alpha2: alpha2,
            noiseStdDev: 1.0);

        var filter = new PairsKalmanFilter(delta: 1e-3, obsNoise: 1.0);
        PairsKalmanFilter.StepResult resultBeforeShift = default;
        PairsKalmanFilter.StepResult resultAtEnd = default;

        // Act
        for (int i = 0; i < totalSteps; i++)
        {
            var res = filter.Update(data[i].Y, data[i].X);
            if (i == shiftStep - 1)
            {
                resultBeforeShift = res;
            }

            if (i == totalSteps - 1)
            {
                resultAtEnd = res;
            }
        }

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(resultBeforeShift.Beta, Is.EqualTo(beta1).Within(0.15));
            Assert.That(resultAtEnd.Beta, Is.EqualTo(beta2).Within(0.15));
        });
    }

    [Test]
    public void Update_DriftingBeta_TracksMovingHedgeRatio()
    {
        // Arrange
        const double startBeta = 1.0;
        const double endBeta = 2.0;
        var data = SyntheticMarketDataGenerator.GenerateDriftingBetaPair(
            steps: 1000,
            startBeta: startBeta,
            endBeta: endBeta,
            noiseStdDev: 0.5);

        var filter = new PairsKalmanFilter(delta: 1e-3, obsNoise: 0.25);
        double earlyBeta = 0;
        double lateBeta = 0;

        // Act
        for (int i = 0; i < data.Count; i++)
        {
            var res = filter.Update(data[i].Y, data[i].X);
            if (i == 100)
            {
                earlyBeta = res.Beta;
            }

            if (i == 999)
            {
                lateBeta = res.Beta;
            }
        }

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(lateBeta, Is.GreaterThan(earlyBeta + 0.7),
                $"Ожидался рост Beta, получено: early={earlyBeta}, late={lateBeta}");
            Assert.That(lateBeta, Is.InRange(endBeta - 0.2, endBeta + 0.1));
        });
    }

    [Test]
    public void Update_OutlierSpike_GeneratesExtremeZScoreAndRecovers()
    {
        // Arrange
        const int spikeStep = 400;
        const double spikeSize = 6.0;
        var data = SyntheticMarketDataGenerator.GenerateWithOutlierSpike(
            steps: 500,
            spikeStep: spikeStep,
            spikeMagnitude: spikeSize,
            noiseStdDev: 1.0);

        var filter = new PairsKalmanFilter(delta: 1e-5, obsNoise: 1.0);
        PairsKalmanFilter.StepResult spikeResult = default;
        PairsKalmanFilter.StepResult nextTickResult = default;

        // Act
        for (int i = 0; i < data.Count; i++)
        {
            var res = filter.Update(data[i].Y, data[i].X);
            if (i == spikeStep)
            {
                spikeResult = res;
            }

            if (i == spikeStep + 1)
            {
                nextTickResult = res;
            }
        }

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(spikeResult.ZScore, Is.GreaterThan(4.0),
                $"ZScore на выбросе слишком мал: {spikeResult.ZScore}");
            Assert.That(nextTickResult.ZScore, Is.InRange(-2.5, 2.5));
        });
    }

    [Test]
    public void Update_DivergingPair_TriggersSustainedHighZScore()
    {
        // Arrange
        const int divergeStep = 300;
        var data = SyntheticMarketDataGenerator.GenerateDivergingPair(
            steps: 400,
            divergeStep: divergeStep);

        var filter = new PairsKalmanFilter(delta: 1e-5, obsNoise: 1.0);
        PairsKalmanFilter.StepResult finalResult = default;

        // Act
        foreach (var tick in data)
        {
            finalResult = filter.Update(tick.Y, tick.X);
        }

        // Assert
        // 4 sigma — общепринятый порог экстремального отклонения;
        // при текущем генераторе расхождение даёт около 4.83.
        Assert.That(finalResult.ZScore, Is.GreaterThan(4.0),
            $"Ожидался экстремальный Z-score при расхождении, получено: {finalResult.ZScore}");
    }

    [Test]
    public void Update_LongRunNumericalStability_NoNaNOrInfinity()
    {
        // Arrange
        var data = SyntheticMarketDataGenerator.GenerateStationaryPair(
            steps: 50_000,
            noiseStdDev: 1e-4);

        var filter = new PairsKalmanFilter(delta: 1e-6, obsNoise: 1e-8);

        // Act & Assert
        foreach (var tick in data)
        {
            var res = filter.Update(tick.Y, tick.X);
            Assert.Multiple(() =>
            {
                Assert.That(res.Beta, Is.Not.NaN.And.Not.EqualTo(double.PositiveInfinity).And.Not.EqualTo(double.NegativeInfinity));
                Assert.That(res.Alpha, Is.Not.NaN.And.Not.EqualTo(double.PositiveInfinity).And.Not.EqualTo(double.NegativeInfinity));
                Assert.That(res.ZScore, Is.Not.NaN.And.Not.EqualTo(double.PositiveInfinity).And.Not.EqualTo(double.NegativeInfinity));
            });
        }
    }
}
