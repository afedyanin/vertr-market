using Microsoft.Extensions.DependencyInjection;

namespace Vertr.Market.Application;

public static class ApplicationRegistrar
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(s => new SignalManager(64));
        services.AddSingleton<MarketDataPeriodicService>();
        services.Configure<MarketDataPeriodicServiceOptions>(o => { });

        return services;
    }
}
