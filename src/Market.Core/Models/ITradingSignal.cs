namespace Market.Core.Models;

public interface ITradingSignal
{
    public string Signal { get; set; }
    public OrderBookDepth BookX { get; set; }
    public OrderBookDepth BookY { get; set; }
}
