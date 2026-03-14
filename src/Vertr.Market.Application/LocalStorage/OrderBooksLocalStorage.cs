using Vertr.Common.Contracts;
using Vertr.Common.Contracts.Abstractions;

namespace Vertr.Market.Application.LocalStorage;

internal sealed class OrderBooksLocalStorage : IOrderBooksLocalStorage, IMarketQuoteProvider
{
    private readonly Dictionary<Guid, OrderBookHistory> _books = [];

    public OrderBook? GetById(Guid instrumentId)
    {
        _books.TryGetValue(instrumentId, out var orderBook);
        return orderBook;
    }

    public void Update(OrderBook orderBook)
    {
        _books[orderBook.InstrumentId] = orderBook;
    }

    public Quote? GetMarketQuote(Guid instrumentId)
    {
        if (!_books.TryGetValue(instrumentId, out var orderBook))
        {
            return null;
        }

        return new Quote
        {
            Time = orderBook.UpdatedAt,
            Bid = orderBook.MaxBid,
            Ask = orderBook.MinAsk,
        };
    }
}

internal sealed class OrderBookHistory
{
    private readonly SortedDictionary<DateTime, SortedList<DateTime, OrderBook>> _orderBooks = [];
    private readonly Lock _lock = new Lock();

    public void Add(OrderBook orderBook)
    {
        var key = GetKey(orderBook);

        lock (_lock)
        {
            _orderBooks.TryGetValue(key, out var list);

            if (list == null)
            {
                list = [];
                _orderBooks.Add(key, list);
            }

            list.Add(orderBook.UpdatedAt, orderBook);
        }
    }

    public OrderBook? GetLast()
    {
        if (!_orderBooks.Any())
        {
            return null;
        }

        lock (_lock)
        {
            if (!_orderBooks.Any())
            {
                return null;
            }

            return _orderBooks.Last().Value.Last().Value;
        }
    }



    private DateTime GetKey(OrderBook ob)
        => new DateTime(
            ob.UpdatedAt.Year,
            ob.UpdatedAt.Month,
            ob.UpdatedAt.Day,
            ob.UpdatedAt.Hour,
            ob.UpdatedAt.Minute,
            0);
}

