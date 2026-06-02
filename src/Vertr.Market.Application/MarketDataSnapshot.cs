namespace Vertr.Market.Application;

/// <summary>
/// Provides read-only access to a captured snapshot of market data.
/// Owns its own backing array so it is independent of the SignalManager's buffers.
/// </summary>
public sealed class MarketDataSnapshot
{
    private readonly double[] _data;

    /// <summary>
    /// Creates a new MarketDataSnapshot with the specified capacity.
    /// </summary>
    /// <param name="capacity">The number of signal slots.</param>
    public MarketDataSnapshot(int capacity)
    {
        _data = new double[capacity];
    }

    /// <summary>
    /// Copies the source span data into this snapshot's internal array.
    /// </summary>
    /// <param name="source">The source span to copy from.</param>
    public void CopyFrom(ReadOnlySpan<double> source)
    {
        source.CopyTo(_data);
    }

    /// <summary>
    /// Gets the total capacity of the snapshot.
    /// </summary>
    public int TotalCapacity => _data.Length;

    /// <summary>
    /// Gets the value at the specified index.
    /// </summary>
    /// <param name="index">The index of the signal value.</param>
    /// <returns>The signal value at the given index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is negative or >= TotalCapacity.</exception>
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

    /// <summary>
    /// Copies the snapshot data into the destination span.
    /// </summary>
    /// <param name="destination">The span to copy data into. Must be at least TotalCapacity in length.</param>
    /// <exception cref="ArgumentException">Thrown when destination is shorter than TotalCapacity.</exception>
    public void CopyTo(Span<double> destination)
    {
        if (destination.Length < TotalCapacity)
        {
            throw new ArgumentException($"Destination span must be at least {TotalCapacity} elements.", nameof(destination));
        }

        _data.CopyTo(destination);
    }

    /// <summary>
    /// Returns a string representation of the snapshot data.
    /// </summary>
    /// <returns>A string representation of the snapshot data.</returns>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('[');
        for (var i = 0; i < _data.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(_data[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (i >= 9)
            {
                sb.Append(", ...");
                break;
            }
        }

        sb.Append(']');
        return sb.ToString();
    }
}
