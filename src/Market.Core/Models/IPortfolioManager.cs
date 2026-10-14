namespace Market.Core.Models;

public interface IPortfolioManager
{
    public IEnumerable<Portfolio> Portfolios { get; }

    public Portfolio GetOrCreate(string sourceId);

    public void Update(IEnumerable<MyTrade> trades);
}
