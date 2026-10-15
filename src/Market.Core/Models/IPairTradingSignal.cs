namespace Market.Core.Models;

public interface IPairTradingSignal
{
    public string SourceId { get; }
    public OrderBookDepth BookX { get; }
    public OrderBookDepth BookY { get; }
    public decimal XQty { get; }
    public decimal YQty { get; }
    public Dictionary<string, double> Params { get; }
}
