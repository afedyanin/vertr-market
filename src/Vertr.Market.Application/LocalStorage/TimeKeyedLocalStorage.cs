using Vertr.Common.Contracts.Abstractions;

namespace Vertr.Market.Application.LocalStorage;

internal sealed class TimeKeyedLocalStorage<T> : IDisposable, ITimeKeyedLocalStorage<T> where T : class, ITimeKeyedItem
{
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
    private readonly Dictionary<Guid, LinkedList<T>> _items = [];

    public IEnumerable<Guid> GetAllKeys() => _items.Keys;

    public T? GetLast(Guid key)
    {
        _lock.EnterReadLock();
        try
        {
            _items.TryGetValue(key, out var list);
            return list?.Last?.Value;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public bool Add(Guid key, T item)
    {
        _lock.EnterWriteLock();

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
            _lock.ExitWriteLock();
        }
    }

    public IEnumerable<T> RemoveBefore(Guid key, DateTime time)
    {
        _lock.EnterWriteLock();

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
            _lock.ExitWriteLock();
        }
    }

    public void Dispose() => _lock.Dispose();
}