using Market.Core.Models;

namespace Market.Core.Abstractions;

public interface ITradeTickStore
{
    public void Set(TradeTick[] items);

    public TradeTick[] Get(int count = 1);

    public int Delete(ushort assetId);
}
