using Microsoft.Extensions.DependencyInjection;

namespace Vertr.Market.Application;

public static class ApplicationRegistrar
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
