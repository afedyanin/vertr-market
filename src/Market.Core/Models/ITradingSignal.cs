namespace Market.Core.Models;

public interface ITradingSignal
{
    public string SourceId { get; }
    public string Signal { get; }
    public OrderBookDepth BookX { get; }
    public OrderBookDepth BookY { get; }
}
