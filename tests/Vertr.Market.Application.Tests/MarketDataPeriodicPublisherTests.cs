using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Vertr.Market.Application.Tests;

public class MarketDataPeriodicPublisherTests
{
    private IServiceCollection _services;
#pragma warning disable NUnit1032 // An IDisposable field/property should be Disposed in a TearDown method
    private IServiceProvider _serviceProvider;
#pragma warning restore NUnit1032 // An IDisposable field/property should be Disposed in a TearDown method

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _services = new ServiceCollection();
        _services.AddApplication();
        _services.AddLogging(builder => builder.AddConsole());

        _serviceProvider = _services.BuildServiceProvider();
    }

    [Test]
    public async Task CanStartPublisher()
    {
        var publisher = _serviceProvider.GetRequiredService<MarketDataPeriodicPublisher>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await publisher.ExecuteAsync(cts.Token);

        Assert.Pass();
    }

    [Test]
    public async Task CanWriteDataAsync()
    {
        var publisher = _serviceProvider.GetRequiredService<MarketDataPeriodicPublisher>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var tasks = new List<Task>
        {
            publisher.ExecuteAsync(cts.Token)
        };

        for (var i = 0; i < 3; i++)
        {
            var minIndex = i * 10;
            var maxIndex = minIndex + 10;
            var value = 100 * i + (.045 + i * 0.1);
            tasks.Add(DoWriteData(minIndex, maxIndex, value, cts.Token));
        }

        await Task.WhenAll(tasks);

        Assert.Pass();
    }

    private async Task DoWriteData(int minIndex, int maxIndex, double value, CancellationToken cancellationToken)
    {
        var snapshotManager = _serviceProvider.GetRequiredService<MarketDataSnapshotManager>();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                snapshotManager.WriteData(Random.Shared.Next(minIndex, maxIndex), value);
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100)), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
