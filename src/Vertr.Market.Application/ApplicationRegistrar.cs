using Microsoft.Extensions.DependencyInjection;
using Vertr.Common.Contracts;
using Vertr.Common.Contracts.Abstractions;
using Vertr.Market.Application.LocalStorage;

namespace Vertr.Market.Application;

public static class ApplicationRegistrar
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IInstrumentsLocalStorage, InstrumentsLocalStorage>();
        services.AddSingleton<ITimeKeyedLocalStorage<OrderBook>, TimeKeyedLocalStorage<OrderBook>>();
        services.AddSingleton<ITimeKeyedLocalStorage<MarketTrade>, TimeKeyedLocalStorage<MarketTrade>>();

        return services;
    }
}
