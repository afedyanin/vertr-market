using Market.Core.Abstractions;
using Market.Core.Models;

namespace Market.Core.ObjectStore;

internal sealed class MarketDepthStore : IMarketDepthStore
{
    public MarketDepth[] Get(int count = 1)
    {
        throw new NotImplementedException();
    }

    public void Set(MarketDepth[] items)
    {
        throw new NotImplementedException();
    }
    public int Delete(ushort assetId)
    {
        throw new NotImplementedException();
    }
}
