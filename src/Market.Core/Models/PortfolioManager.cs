using System.Collections.Concurrent;

namespace Market.Core.Models;

public class PortfolioManager : IPortfolioManager
{
    private readonly ConcurrentDictionary<int, Portfolio> _portfolios = [];

    public IEnumerable<Portfolio> Portfolios => _portfolios.Values;
    public Portfolio GetOrCreate(int strategyId)
    {
        // GetOrAdd возвращает именно тот экземпляр, который лежит в словаре.
        // Раньше метод возвращал локальный объект, даже если гонку создания выиграл другой поток,
        // что приводило к обновлению позиций в «потерянном» экземпляре портфеля.
        return _portfolios.GetOrAdd(strategyId, static id => new Portfolio
        {
            StrategyId = id
        });
    }

    public void Update(IEnumerable<MyTrade> trades)
    {
        //foreach (var trade in trades)
        {
            // TODO: Нужен другой способ матчинга
            // var portfolio = GetOrCreate(trade.SourceId); 
            // portfolio.Update(trade.AssetId, trade.Qty, trade.Price);
        }
    }
}
