namespace Market.Core.Abstractions;

public interface IObjectStore<T> where T : struct
{
    public void Set(T[] items);

    public T[] Get(ushort key, int count = 1);

    public bool Delete(ushort key);

    public void Clear();

    public (long setCount, long getCount, long deleteCount) GetStatistics();
}

