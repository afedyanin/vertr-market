namespace Market.Core.Models;

public interface IPairTradingAlgo
{
    public Guid StrategyId { get; }
    public ushort AssetX { get; }
    public ushort AssetY { get; }

    public IPairTradingSignal? ProcessBook(OrderBookDepth book);
}
