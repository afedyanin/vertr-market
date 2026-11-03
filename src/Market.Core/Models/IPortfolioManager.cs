namespace Market.Core.Models;

public interface IPortfolioManager
{
    public IEnumerable<Portfolio> Portfolios { get; }

    public Portfolio GetOrCreate(int strategyId);

    public void Update(IEnumerable<MyTrade> trades);
}
