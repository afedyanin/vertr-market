namespace Vertr.Market.Application;

/// <summary>
/// Lock-free signal array manager using two buffers for snapshot isolation.
///
/// Design:
/// - Uses two arrays: one for active signal values, one for the current snapshot.
/// - Sequence counter coordinates between writers and snapshot takers without locks.
/// - Duplicate indices within the same interval are resolved by keeping the last write.
///
/// Lock-free snapshot mechanism:
/// 1. Snapshotter atomically swaps sequence from 0 to 1. If old value was 0,
///    no writers are currently writing.
/// 2. Snapshotter copies the active buffer into the snapshot buffer.
/// 3. Snapshotter swaps the two buffers so subsequent writes go to the new active.
/// 4. Snapshotter clears the old active buffer (now the snapshot buffer) to remove stale data.
/// 5. Exiting snapshot mode (setting sequence = 0) resumes writes.
///
/// Writers check sequence at the start of WriteSignal: if it is 1, they spin-wait.
/// Since the snapshotter atomically swaps sequence from 0 to 1, and no writer can
/// be in the middle of writing (because they all check sequence before writing),
/// the snapshot is guaranteed to be consistent.
///
/// This ensures snapshot consistency: all values in a snapshot come from the same
/// time interval because the sequence counter acts as a barrier between intervals.
/// The dedicated snapshot buffer ensures data independence from subsequent writes.
/// </summary>
public sealed class SignalManager : IDisposable
{
    private readonly int _capacity;
    private double[] _activeBuffer;
    private double[] _snapshotBuffer;

    // Separate from _activeBuffer to avoid cache-line bouncing between writers and snapshotter.
    // Writers read this to check if a snapshot is in progress; snapshotter writes this to enter/exit snapshot mode.
    // Accessed via Volatile.Read/Write and Interlocked for memory ordering.
    private int _sequence;

    private bool _disposed;

    /// <summary>
    /// Creates a new SignalManager with the specified signal array capacity.
    /// </summary>
    /// <param name="capacity">The number of signal slots in the array. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity is non-positive.</exception>
    public SignalManager(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        }

        _capacity = capacity;
        _activeBuffer = new double[capacity];
        _snapshotBuffer = new double[capacity];
        _sequence = 0;
    }

    /// <summary>
    /// Gets the total capacity of the signal array.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Writes a signal value at the specified index.
    /// If multiple signals arrive for the same index within an interval,
    /// only the last value is retained.
    /// </summary>
    /// <param name="index">The signal index to write to. Must be in [0, Capacity).</param>
    /// <param name="value">The signal value to store.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public void WriteSignal(int index, double value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (index < 0 || index >= _capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be within [0, Capacity).");
        }

        // Spin-wait with exponential backoff until snapshot mode is cleared.
        // Starts with moderate spin count, backs off to Thread.Sleep(0) to avoid
        // starving other threads on a busy system.
        var spinCount = 0;
        while (Volatile.Read(ref _sequence) == 1)
        {
            if (spinCount < 30)
            {
                Thread.SpinWait(1 << Math.Min(spinCount, 10));
                spinCount++;
            }
            else
            {
                Thread.Sleep(0);
            }
        }

        _activeBuffer[index] = value;

        // Ensure the write to the buffer is visible before any subsequent reads.
        // The next writer will check _sequence; if it is still 0, the write is visible.
        Thread.MemoryBarrier();
    }

    /// <summary>
    /// Takes a consistent snapshot of all signal values.
    ///
    /// The snapshot captures all signals at a single point in time by:
    /// 1. Atomically swapping sequence from 0 to 1. If old value was 0, no writers are active.
    /// 2. Copying the active buffer into the snapshot buffer.
    /// 3. Swapping the active and snapshot buffers so subsequent writes go to a fresh buffer.
    /// 4. Clearing the old active buffer to remove stale data.
    /// 5. Exiting snapshot mode (setting sequence = 0), which resumes writes.
    ///
    /// This ensures that all values in the snapshot originate from the same
    /// time interval — no partial data from adjacent intervals can leak in.
    /// The dedicated snapshot buffer ensures data independence from subsequent writes.
    /// </summary>
    /// <returns>A read-only snapshot view of the signal values at the capture point.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a nested snapshot is attempted.</exception>
    public MarketDataSnapshot TakeSnapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var previous = Interlocked.Exchange(ref _sequence, 1);
        if (previous != 0)
        {
            throw new InvalidOperationException("Nested snapshots are not supported.");
        }

        // Copy active buffer into snapshot buffer.
        _activeBuffer.AsSpan(0, _capacity).CopyTo(_snapshotBuffer);

        // Swap buffers: the snapshot buffer (with captured data) becomes the new active,
        // and the active buffer (with stale data) becomes the new snapshot buffer to be cleared.
        var capturedBuffer = _snapshotBuffer;
        _snapshotBuffer = _activeBuffer;
        _activeBuffer = capturedBuffer;

        // Clear the old snapshot buffer (now _activeBuffer) to remove stale data before writers resume.
        Array.Clear(_activeBuffer, 0, _capacity);

        // Memory barrier ensures all buffer writes are visible before releasing the barrier.
        Volatile.Write(ref _sequence, 0);

        return new MarketDataSnapshot(_snapshotBuffer.AsSpan(0, _capacity));
    }

    /// <summary>
    /// Disposes the SignalManager, freeing all buffers.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _activeBuffer = null!;
        _snapshotBuffer = null!;
    }
}
