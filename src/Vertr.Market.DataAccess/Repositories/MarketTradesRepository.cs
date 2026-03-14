using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vertr.Common.Contracts;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.DataAccess.Dbos;

namespace Vertr.Market.DataAccess.Repositories;

internal sealed class MarketTradesRepository : RepositoryBase, IMarketTradeRepository
{
    public MarketTradesRepository(IDbContextFactory<MarketDataDbContext> contextFactory) : base(contextFactory)
    {
    }
    public async Task<bool> Save(DateTime timeBefore, IEnumerable<MarketTrade> marketTrades)
    {
        if (!marketTrades.Any())
        {
            return false;
        }

        var first = marketTrades.First();

        using var context = await GetDbContext();

        var dbo = new MarketTradeDbo
        {
            Id = Guid.NewGuid(),
            TimeUtc = timeBefore,
            InstrumentId = first.InstrumentId,
            JsonContent = JsonSerializer.Serialize(marketTrades, JsonOptions.DefaultOptions)
        };

        context.Trades.Add(dbo);
        var savedRecords = await context.SaveChangesAsync();
        return savedRecords > 0;
    }
    /*
    public async Task<MarketTrade[]> GetAll()
    {
        using var context = await GetDbContext();

        var res = await context
            .Trades
            .OrderBy(x => x.InstrumentId)
            .ToArrayAsync();

        // TODO: Implement this
        return [];
    }

    public async Task<int> Delete(Guid id)
    {
        using var context = await GetDbContext();

        return await context.Trades
            .Where(t => t.Id == id)
            .ExecuteDeleteAsync();
    }*/
}
