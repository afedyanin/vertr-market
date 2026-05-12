using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;
using ScottPlot;

namespace Vertr.Market.Application.Tests.Kalman;

public class GHFilterTests
{
    [Test]
    public void CanGenerateRandomValues()
    {
        var data = new double[1_000_000];
        var rng = new MersenneTwister(42);
        Normal.Samples(rng, data, mean: 0.0, stddev: 1.0);

        Console.WriteLine(string.Join(',', data.Select(t => $"{t:F4}").Take(20)));
    }

    [Test]
    public void CanGenerateData()
    {
        var dataY = GenData(0, 1, 30, 1);
        Console.WriteLine(string.Join(',', dataY.Select(t => $"{t:F4}")));

        var plt = new Plot();
        plt.Add.Signal([.. dataY]);
        plt.SavePng("measurements.png", 600, 400);
    }


    private IEnumerable<double> GenData(double x0, double dx, int count, double noiseFactor)
        => Enumerable.Range(0, count).Select(i => x0 + dx * i + Normal.Sample(0.0, 1.0) * noiseFactor);

    private IEnumerable<double> GhFilter(IEnumerable<double> data, double x0, double dx, double g, double h, double dt = 0.1)
    {
        /*
    x_est = x0
    results = []
    for z in data:
        # prediction step
        x_pred = x_est + (dx*dt)
        dx = dx

        # update step
        residual = z - x_pred
        dx = dx + h * (residual) / dt
        x_est = x_pred + g * residual
        results.append(x_est)
    return np.array(results)         
         
        */

        // TODO: Implement this
        return [];
    }
}
