namespace Market.Core.Models;

public interface ITradingSignal
{
    public string SourceId { get; }
    public PairTradingAction Action { get; }
    public OrderBookDepth BookX { get; }
    public OrderBookDepth BookY { get; }
}
