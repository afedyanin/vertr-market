using Market.Core.Models;

namespace Market.Core.ObjectStore;

internal sealed class TradeTickObjectStore : ObjectStoreBase<TradeTick>
{
    protected override ushort GetKey(TradeTick item) => item.AssetId;
    protected override long GetTimestamp(TradeTick item) => item.MicrosecondTimestamp;
    public TradeTickObjectStore(long dicreteIntervalMs = 1, int assetItemsMaxLimit = 1000) : base(dicreteIntervalMs, assetItemsMaxLimit)
    {
    }
}
