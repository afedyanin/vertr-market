using System.Collections.Concurrent;

namespace Market.Core.Models;

public class PortfolioManager : IPortfolioManager
{
    private readonly ConcurrentDictionary<string, Portfolio> _portfolios = [];

    public IEnumerable<Portfolio> Portfolios => _portfolios.Values;
    public Portfolio GetOrCreate(string sourceId)
    {
        if (!_portfolios.TryGetValue(sourceId, out var portfolio))
        {
            portfolio = new Portfolio
            {
                SourceId = sourceId
            };

            _portfolios.AddOrUpdate(portfolio.SourceId, portfolio, (key, oldValue) => portfolio);
        }

        return portfolio;

    }

    public void Update(IEnumerable<MyTrade> trades)
    {
        foreach (var trade in trades)
        {
            var portfolio = GetOrCreate(trade.SourceId);
            portfolio.Update(trade.AssetId, trade.Qty, trade.Price);
        }
    }
}
