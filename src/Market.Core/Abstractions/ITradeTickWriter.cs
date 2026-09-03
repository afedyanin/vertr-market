using Market.Core.Models;

namespace Market.Core.Abstractions;

public interface ITradeTickWriter
{
    public void Write(in TradeTick item);
}
