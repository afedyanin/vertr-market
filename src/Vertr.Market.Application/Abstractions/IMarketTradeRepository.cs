using Vertr.Common.Contracts;

namespace Vertr.Market.Application.Abstractions;

public interface IMarketTradeRepository
{
    public Task<bool> Save(DateTime timeBefore, IEnumerable<MarketTrade> marketTrades);
}
