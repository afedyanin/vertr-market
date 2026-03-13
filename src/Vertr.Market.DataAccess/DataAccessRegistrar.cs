using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.DataAccess.Repositories;

namespace Vertr.Market.DataAccess;

public static class DataAccessRegistrar
{
    public static IServiceCollection AddMarketDataAccess(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IDbConnectionFactory>(sp => new DbConnectionFactory(connectionString!));
        services.AddDbContextFactory<MarketDataDbContext>(options => options.UseNpgsql(connectionString));

        services.AddSingleton<IMarketTradesRepository, MarketTradesRepository>();
        services.AddSingleton<IOrderBookRepository, OrderBookRepository>();

        return services;
    }
}
