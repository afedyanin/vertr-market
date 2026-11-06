using Market.Core.Models;

namespace Market.Core.Repositories;

public interface IPortfolioRepository
{
    public IEnumerable<Portfolio> Portfolios { get; }

    public Portfolio GetOrCreate(Guid strategyId);
}
