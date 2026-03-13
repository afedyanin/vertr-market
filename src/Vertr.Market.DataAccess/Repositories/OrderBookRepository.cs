using Microsoft.EntityFrameworkCore;
using Vertr.Market.Application.Abstractions;

namespace Vertr.Market.DataAccess.Repositories;

internal sealed class OrderBookRepository : RepositoryBase, IOrderBookRepository
{
    public OrderBookRepository(IDbContextFactory<MarketDataDbContext> contextFactory) : base(contextFactory)
    {
    }
}
