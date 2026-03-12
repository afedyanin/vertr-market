using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<OrderBooksLocalStorage>();
        services.AddSingleton<IOrderBooksLocalStorage>(sp => sp.GetRequiredService<OrderBooksLocalStorage>());
        services.AddSingleton<IMarketQuoteProvider>(sp => sp.GetRequiredService<OrderBooksLocalStorage>());

        return services;
    }
}
