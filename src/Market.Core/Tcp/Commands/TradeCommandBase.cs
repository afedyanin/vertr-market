using Market.Core.Abstractions;
using Market.Core.Models;

namespace Market.Core.Tcp.Commands;

public abstract class TradeCommandBase : CommandBase
{
    protected IObjectStore<TradeTick> TradesStore { get; private set; }

    protected TradeCommandBase(IObjectStore<TradeTick> tradesStore)
    {
        TradesStore = tradesStore;
    }
}

