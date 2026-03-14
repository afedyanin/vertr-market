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
        services.AddSingleton<IIndexRatesRepository, IndexRatesLocalStorage>();
        services.AddSingleton<IFutureInfoRepository, FutureInfoLocalStorage>();
        services.AddSingleton<ITimeKeyedLocalStorage<OrderBook>, TimeKeyedLocalStorage<OrderBook>>();
        services.AddSingleton<ITimeKeyedLocalStorage<MarketTrade>, TimeKeyedLocalStorage<MarketTrade>>();
        services.AddSingleton<ITimeKeyedLocalStorage<OpenInterest>, TimeKeyedLocalStorage<OpenInterest>>();

        return services;
    }
}
