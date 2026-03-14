using Vertr.Common.Contracts;

namespace Vertr.Market.Application.Abstractions;

public interface IOrderBookRepository
{
    public Task<bool> Save(DateTime timeBefore, IEnumerable<OrderBook> orderBooks);
}
