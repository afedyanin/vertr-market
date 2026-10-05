namespace Market.Core.Models;

public interface IPairTradingAlgo
{
    public PairTradingSignal? ProcessTickX(OrderBookDepth depthX);

    public PairTradingSignal? ProcessTickY(OrderBookDepth depthY);
}
