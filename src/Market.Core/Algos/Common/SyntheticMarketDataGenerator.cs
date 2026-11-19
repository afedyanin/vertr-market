namespace Market.Core.Algos.Common;

public static class SyntheticMarketDataGenerator
{
    public readonly record struct PriceTick(double Y, double X, double TrueBeta, double TrueAlpha);

    /// <summary>
    /// Генерирует нормальное распределение N(mean, stdDev) методом Бокса-Мюллера.
    /// </summary>
    private static double NextGaussian(Random rng, double mean = 0.0, double stdDev = 1.0)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdDev * randStdNormal;
    }

    /// <summary>
    /// 1. Стационарная коинтеграция: Y = trueBeta * X + trueAlpha + noise.
    /// X движется как геометрическое броуновское движение (GBM).
    /// </summary>
    public static List<PriceTick> GenerateStationaryPair(
        int steps,
        double startX = 100.0,
        double trueBeta = 1.5,
        double trueAlpha = 10.0,
        double noiseStdDev = 1.0,
        int seed = 42)
    {
        var rng = new Random(seed);
        var result = new List<PriceTick>(steps);
        double x = startX;

        for (int i = 0; i < steps; i++)
        {
            // Случайное блуждание для X (волатильность ~1% за шаг)
            x *= Math.Exp(NextGaussian(rng, 0.0, 0.01));
            double noise = NextGaussian(rng, 0.0, noiseStdDev);
            double y = trueBeta * x + trueAlpha + noise;

            result.Add(new PriceTick(y, x, trueBeta, trueAlpha));
        }

        return result;
    }

    /// <summary>
    /// 2. Смена режима (Regime Shift): в середине выборки скачкообразно меняются Beta и Alpha.
    /// </summary>
    public static List<PriceTick> GenerateRegimeShiftPair(
        int steps,
        int shiftStep,
        double beta1 = 1.2, double alpha1 = 5.0,
        double beta2 = 2.0, double alpha2 = -15.0,
        double noiseStdDev = 1.0,
        int seed = 42)
    {
        var rng = new Random(seed);
        var result = new List<PriceTick>(steps);
        double x = 100.0;

        for (int i = 0; i < steps; i++)
        {
            x *= Math.Exp(NextGaussian(rng, 0.0, 0.01));
            double beta = i < shiftStep ? beta1 : beta2;
            double alpha = i < shiftStep ? alpha1 : alpha2;
            double y = beta * x + alpha + NextGaussian(rng, 0.0, noiseStdDev);

            result.Add(new PriceTick(y, x, beta, alpha));
        }

        return result;
    }

    /// <summary>
    /// 3. Плавный дрейф параметров: Beta линейно или синусоидально меняется во времени.
    /// </summary>
    public static List<PriceTick> GenerateDriftingBetaPair(
        int steps,
        double startBeta = 1.0,
        double endBeta = 2.5,
        double trueAlpha = 5.0,
        double noiseStdDev = 0.8,
        int seed = 42)
    {
        var rng = new Random(seed);
        var result = new List<PriceTick>(steps);
        double x = 100.0;

        for (int i = 0; i < steps; i++)
        {
            x *= Math.Exp(NextGaussian(rng, 0.0, 0.01));
            double progress = (double)i / (steps - 1);
            double currentBeta = startBeta + (endBeta - startBeta) * progress;
            double y = currentBeta * x + trueAlpha + NextGaussian(rng, 0.0, noiseStdDev);

            result.Add(new PriceTick(y, x, currentBeta, trueAlpha));
        }

        return result;
    }

    /// <summary>
    /// 4. Искусственный выброс (Mean Reversion Spike): резкое отклонение спреда для проверки Z-Score.
    /// </summary>
    public static List<PriceTick> GenerateWithOutlierSpike(
        int steps,
        int spikeStep,
        double spikeMagnitude,
        double trueBeta = 1.5,
        double trueAlpha = 0.0,
        double noiseStdDev = 1.0,
        int seed = 42)
    {
        var data = GenerateStationaryPair(steps, 100.0, trueBeta, trueAlpha, noiseStdDev, seed);
        var target = data[spikeStep];

        // Добавляем искусственный шок к цене Y на одном шаге
        data[spikeStep] = target with { Y = target.Y + spikeMagnitude };
        return data;
    }

    /// <summary>
    /// 5. Разрушение коинтеграции (Divergence): после определенного шага Y уходит в независимый тренд.
    /// </summary>
    public static List<PriceTick> GenerateDivergingPair(
        int steps,
        int divergeStep,
        double trueBeta = 1.5,
        double trueAlpha = 0.0,
        int seed = 42)
    {
        var rng = new Random(seed);
        var result = new List<PriceTick>(steps);
        double x = 100.0;
        double divergence = 0.0;

        for (int i = 0; i < steps; i++)
        {
            x *= Math.Exp(NextGaussian(rng, 0.0, 0.01));
            if (i >= divergeStep)
            {
                // Накопление однонаправленного тренда (расхождение пары)
                divergence += 1.5;
            }

            double y = trueBeta * x + trueAlpha + divergence + NextGaussian(rng, 0.0, 1.0);
            result.Add(new PriceTick(y, x, trueBeta, trueAlpha));
        }

        return result;
    }
}
