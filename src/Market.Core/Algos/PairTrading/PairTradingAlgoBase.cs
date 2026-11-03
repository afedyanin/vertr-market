using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

public abstract class PairTradingAlgoBase : IPairTradingAlgo
{
    protected OrderBookDepth? _lastDepthX;
    protected OrderBookDepth? _lastDepthY;

    protected decimal _openXQty;
    protected decimal _openYQty;

    public ushort AssetX { get; }
    public ushort AssetY { get; }

    public int StrategyId { get; }

    public bool HasOpenPosition => _openXQty != 0 || _openYQty != 0;

    protected PairTradingAlgoBase(int strategyId, ushort assetX, ushort assetY)
    {
        StrategyId = strategyId;
        AssetX = assetX;
        AssetY = assetY;
    }

    public virtual IPairTradingSignal? ProcessBook(OrderBookDepth book)
    {
        if (book is null || !book.IsValid)
        {
            return null;
        }

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

    protected abstract IPairTradingSignal? ProcessTickX();
    protected abstract IPairTradingSignal? ProcessTickY();
}
