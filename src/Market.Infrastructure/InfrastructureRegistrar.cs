using Market.Core.Models;
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
}
