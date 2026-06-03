using Microsoft.Extensions.DependencyInjection;

namespace Vertr.Market.Application;

public static class ApplicationRegistrar
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<SignalManager>();
        services.AddSingleton<MarketDataPeriodicService>();
        services.Configure<MarketDataPeriodicServiceOptions>(o => { });

        return services;
    }
}
