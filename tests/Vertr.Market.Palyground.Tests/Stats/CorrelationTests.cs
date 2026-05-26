namespace Vertr.Market.Palyground.Tests.Stats;

[TestFixture(Category = "Unit")]
public class CorrelationTests
{
    [Test]
    public void CanCalculateCorrelation()
    {
        var a1 = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var a2 = new double[] { 2, 4, 8, 12, 20, 30, 45, 59, 78 };
        var a3 = new double[] { 0, 4, 1, 3, 15, 2, 9, 3, 12 };

        var self = CalculatePearsonCorrelation(a1, a1);
        Assert.That(self, Is.EqualTo(1));

        var a1a2 = CalculatePearsonCorrelation(a1, a2);
        Assert.That(a1a2, Is.GreaterThan(0.9));

        var a1a3 = CalculatePearsonCorrelation(a1, a3);
        Assert.That(a1a3, Is.GreaterThan(0.4));

        var rand1 = new double[9];
        var rand2 = new double[9];
        for (var i = 0; i < rand1.Length; i++)
        {
            rand1[i] = Random.Shared.NextDouble();
            rand2[i] = Random.Shared.NextDouble();
        }

        var a1rand1 = CalculatePearsonCorrelation(a1, rand1);
        var rand1rand2 = CalculatePearsonCorrelation(rand1, rand2);
        Console.WriteLine($"a1rand1={a1rand1} rand1rand2={rand1rand2}");
    }

    public static double CalculatePearsonCorrelation(double[] x, double[] y)
    {
        if (x.Length != y.Length)
        {
            throw new ArgumentException("Arrays must be of the same length.");
        }

        var n = x.Length;
        var avgX = x.Average();
        var avgY = y.Average();

        double sumXY = 0, sumX2 = 0, sumY2 = 0;

        for (var i = 0; i < n; i++)
        {
            var diffX = x[i] - avgX;
            var diffY = y[i] - avgY;

            sumXY += diffX * diffY;
            sumX2 += diffX * diffX;
            sumY2 += diffY * diffY;
        }

        return sumXY / Math.Sqrt(sumX2 * sumY2);
    }
}
