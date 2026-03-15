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

    public async IAsyncEnumerable<MarketTrade> Get(Guid instrumentId, DateTime from, DateTime to)
    {
        await using var context = await GetDbContext();

        var booksDbo = context
            .Trades
            .Where(b =>
                b.InstrumentId == instrumentId &&
                b.TimeUtc >= from &&
                b.TimeUtc <= to
            )
            .OrderBy(x => x.TimeUtc);

        foreach (var dbo in booksDbo)
        {
            if (string.IsNullOrEmpty(dbo.JsonContent))
            {
                continue;
            }

            var trades = JsonSerializer.Deserialize<MarketTrade[]>(dbo.JsonContent, JsonOptions.DefaultOptions) ?? [];

            foreach (var trade in trades
                .Where(b => b.TimeUtc >= from && b.TimeUtc <= to)
                .OrderBy(b => b.TimeUtc))
            {
                yield return trade;
            }
        }
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
    public async Task<int> Delete(Guid id)
    {
        using var context = await GetDbContext();

        return await context.Trades
            .Where(t => t.Id == id)
            .ExecuteDeleteAsync();
    }*/
}
