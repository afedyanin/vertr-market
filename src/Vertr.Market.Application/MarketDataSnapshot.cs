using System.Buffers;

namespace Vertr.Market.Application;

public sealed class MarketDataSnapshot
{
    private readonly double[] _data;
    public int TotalCapacity => _data.Length;

    public MarketDataSnapshot(int capacity)
    {
        _data = new double[capacity];
    }

    public void CopyFrom(ReadOnlySpan<double> source)
    {
        source.CopyTo(_data);
    }

    public double this[int index]
    {
        get
        {
            if (index < 0 || index >= TotalCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within [0, TotalCapacity)");
            }

            return _data[index];
        }
    }
}

public sealed class MarketDataSnapshot<T> : IDisposable
{
    private readonly T[] _data;
    public int TotalCapacity => _data.Length;

    public MarketDataSnapshot(int capacity)
    {
        _data = ArrayPool<T>.Shared.Rent(capacity);
    }

    public void CopyFrom(ReadOnlySpan<T> source)
    {
        source.CopyTo(_data);
    }

    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= TotalCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within [0, TotalCapacity)");
            }

            return _data[index];
        }
    }

    public void Dispose() => ArrayPool<T>.Shared.Return(_data);
}