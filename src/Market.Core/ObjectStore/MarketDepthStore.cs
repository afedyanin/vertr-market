using Market.Core.Abstractions;
using Market.Core.Models;

namespace Market.Core.ObjectStore;

internal sealed class MarketDepthStore : ObjectStore<MarketDepth>, IMarketDepthStore
{
    protected override ushort GetAssetId(MarketDepth item) => item.AssetId;
    protected override long GetTime(MarketDepth item) => item.MicrosecondTimestamp;
}
