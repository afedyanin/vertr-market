using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Vertr.Market.DataAccess;

internal class MarketDataDbContextFactory : IDesignTimeDbContextFactory<MarketDataDbContext>
{
    private const string LocalConnection = "Server=localhost;Port=5432;User Id=postgres;Password=admin;Database=market_data;";

    public MarketDataDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MarketDataDbContext>();

        optionsBuilder.UseNpgsql(
            LocalConnection, b => b.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName));

        return new MarketDataDbContext(optionsBuilder.Options);
    }
}
