using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

public abstract class PairTradingAlgoBase : IPairTradingAlgo
{
    protected OrderBookDepth? _lastDepthX;
    protected OrderBookDepth? _lastDepthY;
    protected ushort _assetX;
    protected ushort _assetY;


    public virtual ITradingSignal? ProcessTickX(OrderBookDepth depthX)
    {
        _assetX = depthX.AssetId;
        _lastDepthX = depthX;
        return ProcessTick(depthX.Timestamp);
    }

    public virtual ITradingSignal? ProcessTickY(OrderBookDepth depthY)
    {
        _assetY = depthY.AssetId;
        _lastDepthY = depthY;
        return ProcessTick(depthY.Timestamp);
    }

    protected abstract ITradingSignal? ProcessTick(DateTime timestamp);
}
