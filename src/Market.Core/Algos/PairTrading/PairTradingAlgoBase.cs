using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

public abstract class PairTradingAlgoBase : IPairTradingAlgo
{
    protected OrderBookDepth? _lastDepthX;
    protected OrderBookDepth? _lastDepthY;
    public ushort AssetX { get; }
    public ushort AssetY { get; }

    protected StrategyState _currentState = StrategyState.None;

    protected PairTradingAlgoBase(ushort assetX, ushort assetY)
    {
        AssetX = assetX;
        AssetY = assetY;
    }

    public virtual ITradingSignal? ProcessBook(OrderBookDepth book)
    {
        if (book.AssetId == AssetX)
        {
            _lastDepthX = book;
            return ProcessTickX();
        }
        else if (book.AssetId == AssetY)
        {
            _lastDepthY = book;
            return ProcessTickY();
        }
        else
        {
            return null;
        }
    }

    protected abstract ITradingSignal? ProcessTickX();
    protected abstract ITradingSignal? ProcessTickY();
}
