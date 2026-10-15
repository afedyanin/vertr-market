namespace Market.Core.Models;

public interface IOrderExecutionEngine
{
    public IEnumerable<MyTrade> Execute(IPairTradingSignal signal);
}
