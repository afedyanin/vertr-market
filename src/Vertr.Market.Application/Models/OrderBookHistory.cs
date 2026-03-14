using System;
using System.Collections.Generic;
using System.Text;
using Vertr.Common.Contracts;

namespace Vertr.Market.Application.Models;

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
    public IEnumerable<OrderBook> GetAllBefore(DateTime time)
    {
        if (!_orderBooks.Any())
        {
            return [];
        }

        lock (_lock)
        {
            if (!_orderBooks.Any())
            {
                return [];
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