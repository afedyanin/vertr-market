using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vertr.Common.Contracts;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.DataAccess.Dbos;

namespace Vertr.Market.DataAccess.Repositories;

internal sealed class OrderBookRepository : RepositoryBase, IOrderBookRepository
{
    public OrderBookRepository(IDbContextFactory<MarketDataDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async IAsyncEnumerable<OrderBook> Get(Guid instrumentId, DateTime from, DateTime to)
    {
        await using var context = await GetDbContext();

        var booksDbo = context
            .OrderBooks
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

            var books = JsonSerializer.Deserialize<OrderBook[]>(dbo.JsonContent, JsonOptions.DefaultOptions) ?? [];

            foreach (var book in books
                .Where(b => b.TimeUtc >= from && b.TimeUtc <= to)
                .OrderBy(b => b.TimeUtc))
            {
                yield return book;
            }
        }
    }

    public async Task<int?> Save(DateTime timeBefore, OrderBook[] orderBooks)
    {
        if (orderBooks.Length <= 0)
        {
            return null;
        }

        var first = orderBooks.First();
        await using var context = await GetDbContext();

        var dbo = new OrderBookDbo
        {
            Id = Guid.NewGuid(),
            TimeUtc = timeBefore,
            InstrumentId = first.InstrumentId,
            JsonContent = JsonSerializer.Serialize(orderBooks, JsonOptions.DefaultOptions)
        };

        context.OrderBooks.Add(dbo);
        var savedRecords = await context.SaveChangesAsync();
        return savedRecords;
    }
}
