using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

public abstract class PairTradingAlgoBase : IPairTradingAlgo
{
    protected OrderBookDepth? _lastDepthX;
    protected OrderBookDepth? _lastDepthY;

    protected decimal _openXQty;
    protected decimal _openYQty;

    protected Dictionary<string, double> AlgoParams { get; }

    public ushort AssetX { get; }
    public ushort AssetY { get; }
    public Guid StrategyId { get; }

    public bool HasOpenPosition => _openXQty != 0 || _openYQty != 0;

    protected PairTradingAlgoBase(AlgoMetadata metadata)
    {
        StrategyId = metadata.StrategyId;
        AssetX = metadata.AssetX;
        AssetY = metadata.AssetY;
        AlgoParams = metadata.AlgoParams;
    }

    public virtual PairTradingSignal? ProcessBook(OrderBookDepth book)
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

    protected abstract PairTradingSignal? ProcessTickX();
    protected abstract PairTradingSignal? ProcessTickY();
}
