using Vertr.Common.Contracts;

namespace Vertr.Market.Application.Abstractions;

public interface IMarketTradeRepository
{
    public Task<bool> Save(DateTime timeBefore, IEnumerable<MarketTrade> marketTrades);
    public IAsyncEnumerable<MarketTrade> Get(Guid instrumentId, DateTime from, DateTime to);
}
