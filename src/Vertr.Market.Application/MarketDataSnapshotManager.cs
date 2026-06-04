using Microsoft.Extensions.Options;

namespace Vertr.Market.Application;

public sealed class MarketDataSnapshotManager
{
    private readonly int _capacity;
    private readonly double[] _activeBuffer;
    private int _sequence; // 0 = writing allowed, 1 = snapshot in progress

    public MarketDataSnapshotManager(IOptions<MarketDataOptions> options)
    {
        _capacity = options.Value.SnapshotCapacity;
        _activeBuffer = new double[_capacity];
        _sequence = 0;
    }

    public int Capacity => _capacity;

    public void WriteData(int index, double value)
    {
        if (index < 0 || index >= _capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be within [0, Capacity).");
        }

        SpinWait.SpinUntil(() => Volatile.Read(ref _sequence) == 0);

        _activeBuffer[index] = value;
    }

    public void TakeSnapshot(MarketDataSnapshot snapshot)
    {
        var previous = Interlocked.Exchange(ref _sequence, 1);

        if (previous != 0)
        {
            throw new InvalidOperationException("Nested snapshots are not supported.");
        }

        try
        {
            snapshot.CopyFrom(_activeBuffer.AsSpan(0, _capacity));
            _activeBuffer.AsSpan().Clear();
        }
        finally
        {
            Interlocked.Exchange(ref _sequence, 0);
        }
    }
}
