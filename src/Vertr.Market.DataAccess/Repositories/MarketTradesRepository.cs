using Microsoft.EntityFrameworkCore;
using Vertr.Common.Contracts;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.DataAccess.Dbos;

namespace Vertr.Market.DataAccess.Repositories;

internal sealed class MarketTradesRepository : RepositoryBase, IMarketTradesRepository
{
    public MarketTradesRepository(IDbContextFactory<MarketDataDbContext> contextFactory) : base(contextFactory)
    {
    }

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

    public async Task<bool> Save(MarketTrade[] trades)
    {
        using var context = await GetDbContext();

        foreach (var trade in trades)
        {
            // TODO: Implement this
            var dbo = new MarketTradesDbo
            {
                Id = Guid.NewGuid(),
                InstrumentId = trade.InstrumentId,
                TimeUtc = trade.Time,
            };

            context.Trades.Add(dbo);
        }

        var savedRecords = await context.SaveChangesAsync();
        return savedRecords > 0;
    }

    public async Task<int> Delete(Guid id)
    {
        using var context = await GetDbContext();

        return await context.Trades
            .Where(t => t.Id == id)
            .ExecuteDeleteAsync();
    }
}
