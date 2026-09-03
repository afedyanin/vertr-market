using Market.Core.Models;

namespace Market.Core.Abstractions;

public interface IMarketDepthWriter
{
    public void Write(in MarketDepth depth);

}
