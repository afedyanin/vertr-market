using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

public abstract class PairTradingAlgoBase : IPairTradingAlgo
{
    protected OrderBookDepth? _lastDepthX;
    protected OrderBookDepth? _lastDepthY;
    public ushort AssetX { get; }
    public ushort AssetY { get; }

    protected DateTime _lastUpdateXTime = DateTime.MinValue;
    protected DateTime _lastUpdateYTime = DateTime.MinValue;

    protected PairTradingAlgoBase(ushort assetX, ushort assetY)
    {
        AssetX = assetX;
        AssetY = assetY;
    }

    public virtual ITradingSignal? ProcessTick(OrderBookDepth book)
    {
        if (book.AssetId == AssetX)
        {
            _lastDepthX = book;
            _lastUpdateXTime = book.Timestamp;
        }
        else if (book.AssetId == AssetY)
        {
            _lastDepthY = book;
            _lastUpdateYTime = book.Timestamp;
        }
        else
        {
            return null;
        }

        return ProcessTick(book.Timestamp);
    }

    protected abstract ITradingSignal? ProcessTick(DateTime timestamp);
}
