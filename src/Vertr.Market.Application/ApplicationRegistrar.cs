using Microsoft.Extensions.DependencyInjection;

namespace Vertr.Market.Application;

public static class ApplicationRegistrar
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        //services.AddSingleton<IInstrumentsLocalStorage, InstrumentsLocalStorage>();
        //services.AddSingleton<ITimeKeyedLocalStorage<OrderBook>, TimeKeyedLocalStorage<OrderBook>>();
        //services.AddSingleton<ITimeKeyedLocalStorage<MarketTrade>, TimeKeyedLocalStorage<MarketTrade>>();

        return services;
    }
}
