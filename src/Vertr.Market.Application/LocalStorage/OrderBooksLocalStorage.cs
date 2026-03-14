using Vertr.Common.Contracts;
using Vertr.Common.Contracts.Abstractions;

namespace Vertr.Market.Application.LocalStorage;

internal sealed class OrderBooksLocalStorage : IOrderBooksLocalStorage
{
    private readonly Dictionary<Guid, SortedList<DateTime, OrderBook>> _books = [];

    public OrderBook? GetLast(Guid instrumentId)
    {
        _books.TryGetValue(instrumentId, out var bookList);
        return bookList?.Last().Value;
    }

    public IEnumerable<OrderBook> GetAllBefore(Guid instrumentId, DateTime time)
    {
        _books.TryGetValue(instrumentId, out var bookList);

        if (bookList == null || !bookList.Any())
        {
            return [];
        }

        return bookList
            .Where(i => i.Key < time)
            .Select(v => v.Value);
    }

    public int DeleteAllBefore(Guid instrumentId, DateTime time)
    {
        _books.TryGetValue(instrumentId, out var bookList);

        var count = 0;

        if (bookList == null || !bookList.Any())
        {
            return count;
        }

        var item = bookList.First();

        while (item.Key < time && bookList.Any())
        {
            bookList.RemoveAt(0);
            item = bookList.First();
            count++;
        }

        return count;
    }

    public void Add(OrderBook orderBook)
    {
        var key = orderBook.InstrumentId;
        _books.TryGetValue(key, out var list);

        if (list == null)
        {
            list = [];
            _books.Add(key, list);
        }

        list.Add(orderBook.UpdatedAt, orderBook);
    }
}