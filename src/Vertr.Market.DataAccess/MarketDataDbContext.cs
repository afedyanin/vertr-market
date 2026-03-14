using Microsoft.EntityFrameworkCore;
using Vertr.Market.DataAccess.Dbos;
using Vertr.Market.DataAccess.Entities;

namespace Vertr.Market.DataAccess;

internal sealed class MarketDataDbContext : DbContext
{
    public DbSet<OrderBookDbo> OrderBooks { get; set; }

    public DbSet<MarketTradeDbo> Trades { get; set; }

    public DbSet<OpenInterestDbo> OpenInterests { get; set; }

    public MarketDataDbContext(DbContextOptions<MarketDataDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("access_control");

        new OrderBookEntityConfiguration().Configure(modelBuilder.Entity<OrderBookDbo>());
        new MarketTradeEntityConfiguration().Configure(modelBuilder.Entity<MarketTradeDbo>());
        new OpenInterestEntityConfiguration().Configure(modelBuilder.Entity<OpenInterestDbo>());
    }
}
