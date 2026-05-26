using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;
using ScottPlot;

namespace Vertr.Market.Palyground.Tests.Kalman;

public class GhFilterTests
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

    [Test]
    public void CanUseGhFilter()
    {
        var weights = GenData(x0: 160, dx: 1.0, count: 30, noiseFactor: 1);
        var data = GhFilter(weights, x0: 160, dx: 1.0, g: 0.6, h: 0.67, dt: 1.0);

        var plt = new Plot();
        plt.Add.Signal([.. weights]);
        plt.Add.Signal([.. data]);
        plt.SavePng("filtered.png", 600, 400);
    }

    [Test]
    public void CanUseGhFilter2()
    {
        var filter = new GhFilter(x0: 160, dx: 1.0, g: 0.6, h: 0.67, dt: 1.0);
        var weights = GenData(x0: 160, dx: 1.0, count: 30, noiseFactor: 1);
        var data = weights.Select(filter.Next);

        var plt = new Plot();
        plt.Add.Signal([.. weights]);
        plt.Add.Signal([.. data]);
        plt.SavePng("filtered2.png", 600, 400);
    }

    private IEnumerable<double> GenData(double x0, double dx, int count, double noiseFactor)
        => Enumerable.Range(0, count).Select(i => x0 + dx * i + Normal.Sample(0.0, 1.0) * noiseFactor);

    private IEnumerable<double> GhFilter(IEnumerable<double> data, double x0, double dx, double g, double h, double dt = 0.1)
    {
        var results = new List<double>();

        var xEst = x0;
        foreach (var z in data)
        {
            // prediction step
            var xPred = xEst + (dx * dt);
            //dx = dx;

            // update step
            var residual = z - xPred;
            dx += h * residual / dt;
            xEst = xPred + g * residual;

            results.Add(xEst);
        }

        return results;
    }
}

public class GhFilter
{
    private readonly double _x0;
    private readonly double _dx0;
    private readonly double _g;
    private readonly double _h;
    private readonly double _dt;

    private double _xEst;
    private double _dx;

    public GhFilter(double x0, double dx, double g, double h, double dt = 0.1)
    {
        _x0 = x0;
        _g = g;
        _h = h;
        _dt = dt;
        _dx0 = dx;

        Reset();
    }

    public void Reset()
    {
        _xEst = _x0;
        _dx = _dx0;
    }

    public double Next(double z)
    {
        // prediction step
        var xPred = _xEst + (_dx * _dt);
        //dx = dx;

        // update step
        var residual = z - xPred;
        _dx += _h * residual / _dt;
        _xEst = xPred + _g * residual;

        return _xEst;
    }
}
