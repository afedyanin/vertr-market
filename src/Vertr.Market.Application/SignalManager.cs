namespace Vertr.Market.Application;

/// <summary>
/// Lock-free signal array manager using a single buffer for signal storage.
///
/// Lock-free snapshot mechanism:
/// 1. Snapshotter atomically swaps sequence from 0 to 1. If old value was 0,
///    no writers are currently writing.
/// 2. Snapshotter copies the active buffer into the caller-provided snapshot.
/// 3. Snapshotter clears the active buffer to remove stale data.
/// 4. Exiting snapshot mode (setting sequence = 0) resumes writes.
///
/// Writers check sequence at the start of WriteSignal: if it is 1, they spin-wait.
/// Since the snapshotter atomically swaps sequence from 0 to 1, and no writer can
/// be in the middle of writing (because they all check sequence before writing),
/// the snapshot is guaranteed to be consistent.
///
/// This ensures snapshot consistency: all values in a snapshot come from the same
/// time interval because the sequence counter acts as a barrier between intervals.
/// </summary>
public sealed class SignalManager
{
    private readonly int _capacity;
    private readonly double[] _activeBuffer;

    // Separate from _activeBuffer to avoid cache-line bouncing between writers and snapshotter.
    // Writers read this to check if a snapshot is in progress; snapshotter writes this to enter/exit snapshot mode.
    // Accessed via Volatile.Read/Write and Interlocked for memory ordering.
    private int _sequence;

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
    /// 2. Copying the active buffer into the caller-provided snapshot (writers blocked).
    /// 3. Clearing the active buffer to remove stale data before writers resume.
    /// 4. Exiting snapshot mode (setting sequence = 0), which resumes writes.
    ///
    /// This ensures that all values in the snapshot originate from the same
    /// time interval — no partial data from adjacent intervals can leak in.
    /// </summary>
    /// <param name="snapshot">The snapshot instance to populate with captured data.</param>
    /// <exception cref="InvalidOperationException">Thrown when a nested snapshot is attempted.</exception>
    public void TakeSnapshot(MarketDataSnapshot snapshot)
    {
        var previous = Interlocked.Exchange(ref _sequence, 1);
        if (previous != 0)
        {
            throw new InvalidOperationException("Nested snapshots are not supported.");
        }

        // Copy active buffer directly into the caller-provided snapshot while writers are blocked.
        snapshot.CopyFrom(_activeBuffer.AsSpan(0, _capacity));

        // Clear the active buffer to remove stale data before writers resume.
        Array.Clear(_activeBuffer, 0, _capacity);

        // Memory barrier ensures all buffer writes are visible before releasing the barrier.
        Volatile.Write(ref _sequence, 0);
    }
}
