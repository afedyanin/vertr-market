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
