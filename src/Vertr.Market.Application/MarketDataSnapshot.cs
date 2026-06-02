namespace Vertr.Market.Application;

/// <summary>
/// Provides read-only access to a captured snapshot of market data.
/// This is a ref struct to ensure it remains on the stack and cannot be captured by closures or heap allocations.
/// </summary>
public ref struct MarketDataSnapshot
{
    private readonly ReadOnlySpan<double> _data;

    /// <summary>
    /// Creates a new MarketDataSnapshot that wraps the given data span.
    /// </summary>
    /// <param name="data">The span containing the snapshot data.</param>
    public MarketDataSnapshot(ReadOnlySpan<double> data)
    {
        _data = data;
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
