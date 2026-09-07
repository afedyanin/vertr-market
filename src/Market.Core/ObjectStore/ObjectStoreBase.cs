using Market.Core.Abstractions;

namespace Market.Core.ObjectStore;

internal abstract class ObjectStoreBase<T> : IObjectStore<T>, IDisposable where T : struct
{
    private readonly Dictionary<ushort, LinkedList<T>> _store = [];

    private readonly ReaderWriterLockSlim _lock = new();

    private readonly int _assetItemsMaxLimit;

    private long _setCount;
    private long _getCount;
    private long _deleteCount;

    protected abstract ushort GetKey(T item);

    protected abstract long GetTimestamp(T item);

    protected ObjectStoreBase(int assetItemsMaxLimit = 1000)
    {
        _assetItemsMaxLimit = assetItemsMaxLimit;
    }

    public T[] Get(ushort assetId, int count = 1)
    {
        _lock.EnterReadLock();

        try
        {
            Interlocked.Increment(ref _getCount);

            if (!_store.TryGetValue(assetId, out var list))
            {
                return [];
            }

            // TakeLast enumerates the whole list (LinkedList has no indexed access), which is
            // O(items) even for count = 1; walking backwards from the tail keeps it O(count).
            T[] result = new T[Math.Min(count, list.Count)];
            int index = result.Length - 1;
            for (var node = list.Last; node is not null && index >= 0; node = node.Previous)
            {
                result[index--] = node.Value;
            }

            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Set(T[] items)
    {
        _lock.EnterWriteLock();
        try
        {
            Interlocked.Increment(ref _setCount);

            foreach (T item in items)
            {
                var assetId = GetKey(item);
                _store.TryGetValue(assetId, out var list);

                if (list == null)
                {
                    list = new LinkedList<T>();
                    _store.Add(assetId, list);
                }

                // TODO: Implement replace by Timestamp discrete value
                list.AddLast(item);

                if (list.Count > _assetItemsMaxLimit)
                {
                    list.RemoveFirst();
                }
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool Delete(ushort assetId)
    {
        _lock.EnterWriteLock();
        try
        {
            Interlocked.Increment(ref _deleteCount);
            return _store.Remove(assetId);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            Interlocked.Exchange(ref _deleteCount, 0);
            Interlocked.Exchange(ref _getCount, 0);
            Interlocked.Exchange(ref _setCount, 0);

            _store.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public (long setCount, long getCount, long deleteCount) GetStatistics()
    {
        return (_setCount, _getCount, _deleteCount);
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
