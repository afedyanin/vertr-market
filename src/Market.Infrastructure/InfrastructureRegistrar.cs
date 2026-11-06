using Market.Core.Repositories;
using Market.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Market.Infrastructure;

public static class InfrastructureRegistrar
{
    public static IServiceCollection AddAlgoMetadataRepository(this IServiceCollection services)
    {
        services.AddSingleton<IAlgoMetadataRepository, AlgoMetadataRepository>(sp =>
        {
            var repo = new AlgoMetadataRepository();
            // TODO: Init from file

            return repo;
        });

        return services;
    }

    public static IServiceCollection AddPortfolioRepository(this IServiceCollection services)
    {
        services.AddSingleton<IPortfolioRepository, PortfolioRepository>();
        return services;
    }

    public static IServiceCollection AddStaticDataProvider(this IServiceCollection services)
    {
        services.AddSingleton<JsonStaticDataProvider>();
        services.AddSingleton<IStaticDataProvider>(sp => sp.GetRequiredService<JsonStaticDataProvider>());
        return services;
    }
}
