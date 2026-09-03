namespace Market.Core.ObjectStore;

internal abstract class ObjectStore<T> : IDisposable where T : struct
{
    private readonly Dictionary<ushort, LinkedList<T>> _store = [];

    private readonly ReaderWriterLockSlim _lock = new();

    private readonly int _assetItemsMaxLimit;

    private long _setCount;
    private long _getCount;
    private long _deleteCount;

    protected abstract ushort GetAssetId(T item);

    protected abstract long GetTime(T item);

    protected ObjectStore(int assetItemsMaxLimit = 1000)
    {
        _assetItemsMaxLimit = assetItemsMaxLimit;
    }

    public T[] Get(ushort assetId, int count = 1)
    {
        _lock.EnterReadLock();

        try
        {
            Interlocked.Increment(ref _getCount);

            return _store.TryGetValue(assetId, out var list) ?
                [.. list.TakeLast(count)] : [];
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
                var assetId = GetAssetId(item);
                _store.TryGetValue(assetId, out var list);

                if (list == null)
                {
                    list = new LinkedList<T>();
                    _store.Add(assetId, list);
                }

                // TODO: Implement replace by time discrete value
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

    public bool DeleteAsset(ushort assetId)
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
