using System.Text;

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

    public override string ToString()
    {
        var sb = new StringBuilder('[');

        for (var i = 0; i < TotalCapacity; i++)
        {
            if (_data[i] != 0)
            {
                sb.Append($"{i}:{_data[i]:F4}, ");
            }
        }

        sb.Append(']');
        return sb.ToString();
    }
}
