using Market.Core.Models;

namespace Market.Core.Abstractions;

public interface ITradeTickStore
{
    public void Set(TradeTick[] items);

    public TradeTick[] Get(ushort assetId, int count = 1);

    public bool DeleteAsset(ushort assetId);

    public void Clear();
}
