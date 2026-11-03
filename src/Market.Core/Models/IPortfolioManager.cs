namespace Market.Core.Models;

public interface IPortfolioManager
{
    public IEnumerable<Portfolio> Portfolios { get; }

    public Portfolio GetOrCreate(Guid strategyId);

    public void Update(IEnumerable<MyTrade> trades);
}
