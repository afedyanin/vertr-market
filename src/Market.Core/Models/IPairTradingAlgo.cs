namespace Market.Core.Models;

public interface IPairTradingAlgo
{
    public ushort AssetX { get; }
    public ushort AssetY { get; }
    public ITradingSignal? ProcessBook(OrderBookDepth book);
}
