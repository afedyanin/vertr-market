using Vertr.Common.Contracts;

namespace Vertr.Market.Application.Abstractions;

public interface IMarketTradeRepository
{
    public Task<int?> Save(DateTime timeBefore, MarketTrade[] marketTrades);
    public IAsyncEnumerable<MarketTrade> Get(Guid instrumentId, DateTime from, DateTime to);
}
