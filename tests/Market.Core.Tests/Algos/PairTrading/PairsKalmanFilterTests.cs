using Market.Core.Algos.Common;

namespace Market.Core.Tests.Algos.PairTrading;

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
}
