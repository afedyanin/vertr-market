using Disruptor;
using Microsoft.Extensions.DependencyInjection;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.Application.EventHanlers;

namespace Vertr.Market.Application;

public static class ApplicationRegistrar
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.Configure<MarketDataOptions>(o =>
        {
            o.PublishingInterval = TimeSpan.FromMilliseconds(1000);
            o.SnapshotCapacity = 64;
            o.RingBufferSize = 128;
        });

        services.AddSingleton<MarketDataSnapshotManager>();
        services.AddSingleton<MarketDataPeriodicPublisher>();
        services.AddSingleton<IRingBufferProvider<MarketDataSnapshot>, MarketDataRingBufferProvider>();

        services.AddTransient<IEventHandler<MarketDataSnapshot>, MarketDataSnapshotLogger>();

        return services;
    }
}
