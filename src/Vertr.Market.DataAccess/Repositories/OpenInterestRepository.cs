using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vertr.Common.Contracts;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.DataAccess.Dbos;

namespace Vertr.Market.DataAccess.Repositories;

internal sealed class OpenInterestRepository : RepositoryBase, IOpenInterestRepository
{
    public OpenInterestRepository(IDbContextFactory<MarketDataDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async IAsyncEnumerable<OpenInterest> Get(Guid instrumentId, DateTime from, DateTime to)
    {
        await using var context = await GetDbContext();

        var booksDbo = context
            .OpenInterests
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

            var interests = JsonSerializer.Deserialize<OpenInterest[]>(dbo.JsonContent, JsonOptions.DefaultOptions) ?? [];

            foreach (var item in interests
                .Where(b => b.TimeUtc >= from && b.TimeUtc <= to)
                .OrderBy(b => b.TimeUtc))
            {
                yield return item;
            }
        }
    }

    public async Task<bool> Save(DateTime timeBefore, IEnumerable<OpenInterest> openInterests)
    {
        if (!openInterests.Any())
        {
            return false;
        }

        var first = openInterests.First();

        using var context = await GetDbContext();

        var dbo = new OpenInterestDbo
        {
            Id = Guid.NewGuid(),
            TimeUtc = timeBefore,
            InstrumentId = first.InstrumentId,
            JsonContent = JsonSerializer.Serialize(openInterests, JsonOptions.DefaultOptions)
        };

        context.OpenInterests.Add(dbo);
        var savedRecords = await context.SaveChangesAsync();
        return savedRecords > 0;
    }
}
