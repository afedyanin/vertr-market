using Market.Core.Models;

namespace Market.Core.ObjectStore;

internal sealed class MarketDepthObjectStore : ObjectStoreBase<MarketDepth>
{
    protected override ushort GetKey(MarketDepth item) => item.AssetId;
    protected override long GetTimestamp(MarketDepth item) => item.MicrosecondTimestamp;
}
