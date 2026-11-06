using System.Collections.Concurrent;
using Market.Core.Models;
using Market.Core.Repositories;

namespace Market.Infrastructure.Repositories;

internal sealed class PortfolioRepository : IPortfolioRepository
{
    private readonly ConcurrentDictionary<Guid, Portfolio> _portfolios = [];

    public IEnumerable<Portfolio> Portfolios => _portfolios.Values;

    public Portfolio GetOrCreate(Guid strategyId)
        => _portfolios.GetOrAdd(strategyId, static id => new Portfolio
        {
            StrategyId = id
        });
}
