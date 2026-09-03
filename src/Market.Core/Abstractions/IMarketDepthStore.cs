using Market.Core.Models;

namespace Market.Core.Abstractions;

public interface IMarketDepthStore
{
    public void Set(MarketDepth[] items);

    public MarketDepth[] Get(int count = 1);

    public int Delete(ushort assetId);
}
