using Market.Core.Models;

namespace Market.Core.ObjectStore;

internal sealed class MarketDepthObjectStore : ObjectStoreBase<MarketDepth>
{
    protected override ushort GetKey(MarketDepth item) => item.AssetId;
    protected override long GetTimestamp(MarketDepth item) => item.MicrosecondTimestamp;

    public MarketDepthObjectStore(long dicreteIntervalMs = 1, int assetItemsMaxLimit = 1000) : base(dicreteIntervalMs, assetItemsMaxLimit)
    {
    }
}
