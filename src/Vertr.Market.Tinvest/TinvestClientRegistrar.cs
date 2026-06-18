using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tinkoff.InvestApi;

namespace Vertr.Market.Tinvest;

public static class TinvestClientRegistrar
{
    public static IServiceCollection AddTinvestMarketData(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TinvestMarketDataSettings>().BindConfiguration(nameof(TinvestMarketDataSettings));
        services.AddInvestApiClient((_, settings) => configuration.Bind($"{nameof(InvestApiSettings)}", settings));
        services.AddSingleton<MarketDataStreamClient>();

        return services;
    }
}
