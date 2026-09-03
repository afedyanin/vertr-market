using Market.Core.Abstractions;
using Market.Core.Models;

namespace Market.Core.ObjectStore;

internal sealed class TradeTickObjectStore : ObjectStore<TradeTick>, ITradeTickStore
{
    protected override ushort GetAssetId(TradeTick item) => item.AssetId;
    protected override long GetTime(TradeTick item) => item.MicrosecondTimestamp;
}
