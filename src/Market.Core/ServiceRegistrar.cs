using Market.Core.Abstractions;
using Market.Core.Models;
using Market.Core.ObjectStore;
using Microsoft.Extensions.DependencyInjection;

namespace Market.Core;

public static class ServiceRegistrar
{
    public static IServiceCollection AddObjectStores(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IObjectStore<TradeTick>, TradeTickObjectStore>();
        serviceCollection.AddSingleton<IObjectStore<MarketDepth>, MarketDepthObjectStore>();

        return serviceCollection;
    }
}
