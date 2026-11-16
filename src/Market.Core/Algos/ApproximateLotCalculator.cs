namespace Market.Core.Algos;

public static class ApproximateLotCalculator
{
    public static (int lotsX, int lotsY) CalculateBestApproximation(
        double qtyX, double qtyY,
        double lotSizeX, double lotSizeY,
        int maxLots = 100)
    {
        if (qtyX <= 0 || qtyY <= 0 || lotSizeX <= 0 || lotSizeY <= 0)
        {
            throw new InvalidOperationException("All params must be positive.");
        }

        double targetRatio = (qtyX / lotSizeX) / (qtyY / lotSizeY);

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
