using System.Collections.Concurrent;
using Vertr.Common.Contracts.Abstractions;

namespace Vertr.Market.Application.LocalStorage;

internal sealed class TimeKeyedLocalStorage<T> : IDisposable, ITimeKeyedLocalStorage<T> where T : class, ITimeKeyedItem
{
    private readonly Dictionary<Guid, LinkedList<T>> _items = [];
    private readonly ConcurrentDictionary<Guid, ReaderWriterLockSlim> _locks = [];

    public IEnumerable<Guid> GetAllKeys() => _items.Keys;

    public T? GetLast(Guid key)
    {
        if (!_locks.TryGetValue(key, out var keyLock))
        {
            return default;
        }

        keyLock.EnterReadLock();
        try
        {
            _items.TryGetValue(key, out var list);
            return list?.Last?.Value;
        }
        finally
        {
            keyLock.ExitReadLock();
        }
    }

    public bool Add(Guid key, T item)
    {
        _locks.TryGetValue(key, out var keyLock);
        keyLock ??= _locks.AddOrUpdate(key, new ReaderWriterLockSlim(), (key, oldLock) => oldLock);

        keyLock.EnterWriteLock();

        try
        {
            _items.TryGetValue(key, out var list);

            if (list == null)
            {
                list = [];
                list.AddLast(item);
                _items.Add(key, list);
                return true;
            }

            if (list.Any() && list.Last().TimeUtc > item.TimeUtc)
            {
                // skip adding earlier items
                return false;
            }

            list.AddLast(item);
            return true;
        }
        finally
        {
            keyLock.ExitWriteLock();
        }
    }

    public IEnumerable<T> RemoveBefore(Guid key, DateTime time)
    {
        if (!_locks.TryGetValue(key, out var keyLock))
        {
            return [];
        }

        keyLock.EnterWriteLock();

        try
        {
            _items.TryGetValue(key, out var list);

            if (list == null || !list.Any())
            {
                return [];
            }

            var node = list.First;
            var removed = new List<T>();

            while (node != null && node.Value.TimeUtc < time)
            {
                removed.Add(node.Value);
                list.RemoveFirst();
                node = list.First;
            }

            return removed;
        }
        finally
        {
            keyLock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        foreach (var item in _locks.Values)
        {
            item?.Dispose();
        }

        _locks.Clear();
        _items.Clear();
    }
}