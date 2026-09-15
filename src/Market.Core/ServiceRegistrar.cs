using Market.Core.Abstractions;
using Market.Core.Models;
using Market.Core.ObjectStore;
using Microsoft.Extensions.DependencyInjection;

namespace Market.Core;

public static class ServiceRegistrar
{
    public static IServiceCollection AddObjectStores(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IObjectStore<TradeTick>, TradeTickObjectStore>(
            sp => new TradeTickObjectStore(dicreteIntervalMs: 10_000));
        serviceCollection.AddSingleton<IObjectStore<MarketDepth>, MarketDepthObjectStore>(
            sp => new MarketDepthObjectStore(dicreteIntervalMs: 10_000));

        return serviceCollection;
    }
}
