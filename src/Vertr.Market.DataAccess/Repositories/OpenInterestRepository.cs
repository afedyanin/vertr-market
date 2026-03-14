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
