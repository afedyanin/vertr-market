using Vertr.Common.Contracts;

namespace Vertr.Market.Application.Abstractions;

public interface IOrderBookRepository
{
    public Task<int?> Save(DateTime timeBefore, OrderBook[] orderBooks);

    public IAsyncEnumerable<OrderBook> Get(Guid instrumentId, DateTime from, DateTime to);
}
