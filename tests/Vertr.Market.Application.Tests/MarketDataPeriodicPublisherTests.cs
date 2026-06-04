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
}
