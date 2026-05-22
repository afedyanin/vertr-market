namespace Vertr.Market.Application.Abstractions;

public abstract class MarketDataItemBase
{
    public abstract void CopyFrom(MarketDataItemBase source);

    public abstract void Reset();
}
