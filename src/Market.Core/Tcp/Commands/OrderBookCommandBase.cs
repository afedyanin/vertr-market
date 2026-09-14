using Market.Core.Abstractions;
using Market.Core.Models;

namespace Market.Core.Tcp.Commands;

public abstract class OrderBookCommandBase : CommandBase
{
    protected IObjectStore<MarketDepth> BooksStore { get; private set; }

    protected OrderBookCommandBase(IObjectStore<MarketDepth> booksStore)
    {
        BooksStore = booksStore;
    }
}
