using Microsoft.Extensions.Options;

namespace Vertr.Market.Application;

public sealed class MarketDataSnapshotManager : IDisposable
{
    private readonly int _capacity;
    private readonly double[] _activeBuffer;
    private readonly bool _resetBuffer;
    private int _isSnapshotInProgress;
    private readonly ManualResetEventSlim _snapshotCompletedEvent = new(true);

    public MarketDataSnapshotManager(IOptions<MarketDataOptions> options)
    {
        _capacity = options.Value.SnapshotCapacity;
        _resetBuffer = options.Value.ResetBufferAfterPublish;
        _activeBuffer = new double[_capacity];
    }

    public int Capacity => _capacity;

    public void WriteData(int index, double value)
    {
        if (index < 0 || index >= _capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be within [0, Capacity).");
        }

        // Блокируем запись во время снимка, ждём до 30 секунд
        if (!_snapshotCompletedEvent.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Timed out waiting for snapshot to complete.");
        }

        _activeBuffer[index] = value;
    }

    public void TakeSnapshot(MarketDataSnapshot snapshot)
    {
        var previous = Interlocked.Exchange(ref _isSnapshotInProgress, 1);

        if (previous != 0)
        {
            throw new InvalidOperationException("Nested snapshots are not supported.");
        }

        try
        {
            snapshot.CopyFrom(_activeBuffer.AsSpan());
            if (_resetBuffer)
            {
                _activeBuffer.AsSpan().Clear();
            }
        }
        finally
        {
            if (Interlocked.CompareExchange(ref _isSnapshotInProgress, 0, 1) == 1)
            {
                _snapshotCompletedEvent.Set();
            }
        }
    }

    public void Dispose()
    {
        _snapshotCompletedEvent.Dispose();
    }
}

public sealed class MarketDataSnapshotManager<T> : IDisposable where T : class
{
    private readonly int _capacity;
    private readonly T[] _activeBuffer;
    private readonly bool _resetBuffer;
    private int _isSnapshotInProgress;
    private readonly ManualResetEventSlim _snapshotCompletedEvent = new(true);

    public MarketDataSnapshotManager(IOptions<MarketDataOptions> options)
    {
        _capacity = options.Value.SnapshotCapacity;
        _resetBuffer = options.Value.ResetBufferAfterPublish;
        _activeBuffer = new T[_capacity];
    }

    public int Capacity => _capacity;

    /// <summary>
    /// Запись данных в пул. 
    /// Контракт: каждый вызывающий поток работает со своим поддиапазоном индексов.
    /// Пересечений по индексам между потоками нет. Синхронизация на уровне индексов не требуется.
    /// </summary>
    public void WriteData(int index, T value)
    {
        if (index < 0 || index >= _capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be within [0, Capacity).");
        }

        // Блокируем запись во время снимка, ждём до 30 секунд
        if (!_snapshotCompletedEvent.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Timed out waiting for snapshot to complete.");
        }

        _activeBuffer[index] = value;
    }

    public void TakeSnapshot(MarketDataSnapshot<T> snapshot)
    {
        var previous = Interlocked.Exchange(ref _isSnapshotInProgress, 1);
        if (previous != 0)
        {
            throw new InvalidOperationException("Nested snapshots are not supported.");
        }

        try
        {
            snapshot.CopyFrom(_activeBuffer.AsSpan());

            if (_resetBuffer)
            {
                _activeBuffer.AsSpan().Clear();
            }
        }
        finally
        {
            if (Interlocked.CompareExchange(ref _isSnapshotInProgress, 0, 1) == 1)
            {
                _snapshotCompletedEvent.Set();
            }
        }
    }

    public void Dispose()
    {
        _snapshotCompletedEvent.Dispose();
    }
}
