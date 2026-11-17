namespace Market.Core.Algos;

public static class ApproximateLotCalculator
{
    public static (int lotsX, int lotsY) CalculateLots(
        double beta,
        int lotSizeX = 1,
        int lotSizeY = 1,
        int maxLots = 10)
    {
        if (lotSizeX <= 0 || lotSizeY <= 0)
        {
            throw new InvalidOperationException("Lot sizes must be positive.");
        }

        double targetRatio = lotSizeY / (lotSizeX * beta);

        int bestX = 1;
        int bestY = 1;
        double minError = double.MaxValue;

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

                if (minError < 1e-9)
                {
                    break;
                }
            }
        }

        return (bestX, bestY);
    }
}
