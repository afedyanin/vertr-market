namespace Market.Core.Models;

public interface IPairTradingAlgo
{
    public ITradingSignal? ProcessTickX(OrderBookDepth depthX);

    public ITradingSignal? ProcessTickY(OrderBookDepth depthY);
}
