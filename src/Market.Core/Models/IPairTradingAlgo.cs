namespace Market.Core.Models;

public interface IPairTradingAlgo
{
    public ushort AssetX { get; }
    public ushort AssetY { get; }
    public IPairTradingSignal? ProcessBook(OrderBookDepth book);
}
