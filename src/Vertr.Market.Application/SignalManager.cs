namespace Vertr.Market.Application;

public sealed class SignalManager
{
    private readonly int _capacity;
    private readonly double[] _activeBuffer;
    private int _sequence; // 0 = writing allowed, 1 = snapshot in progress

    public SignalManager(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        }

        _capacity = capacity;
        _activeBuffer = new double[capacity];
        _sequence = 0;
    }

    public int Capacity => _capacity;

    public void WriteSignal(int index, double value)
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
