// IDE0005: Using directive is unnecessary - kept for clarity with DI types
#pragma warning disable IDE0005
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
#pragma warning restore IDE0005

namespace Vertr.Market.Application;

public static class ApplicationRegistrar
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SignalManager>();
        services.AddHostedService<MarketDataPeriodicService>();
        services.Configure<MarketDataPeriodicServiceOptions>(o => { });

        return services;
    }
}
