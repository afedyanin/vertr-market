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

    public virtual IPairTradingSignal? ProcessBook(OrderBookDepth book)
    {
        // Односторонний/пустой стакан не содержит валидных цен: пропускаем его,
        // чтобы не упасть на MidPrice!.Value и не «отравить» фильтр значениями NaN.
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
