using Market.Core.Models;

namespace Market.Core.Abstractions;

public interface IMarketDepthStore
{
    public void Set(MarketDepth[] items);

    public MarketDepth[] Get(ushort assetId, int count = 1);

    public bool DeleteAsset(ushort assetId);

    public void Clear();

}

