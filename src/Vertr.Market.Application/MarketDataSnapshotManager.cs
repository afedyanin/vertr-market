using Microsoft.Extensions.Options;

namespace Vertr.Market.Application;

public sealed class MarketDataSnapshotManager
{
    private readonly int _capacity;
    private readonly double[] _activeBuffer;
    private readonly bool _resetBuffer;
    private int _sequence; // 0 = writing allowed, 1 = snapshot in progress

    public MarketDataSnapshotManager(IOptions<MarketDataOptions> options)
    {
        _capacity = options.Value.SnapshotCapacity;
        _resetBuffer = options.Value.ResetBufferAfterPublish;
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

        // Ждём окончания предыдущего снимка с таймаутом 30 секунд
        if (!SpinWait.SpinUntil(() => Volatile.Read(ref _sequence) == 0, TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Snapshot timeout - no data will be written for this interval.");
        }

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
            if (_resetBuffer)
            {
                _activeBuffer.AsSpan().Clear();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _sequence, 0);
        }
    }
}

public sealed class MarketDataSnapshotManager<T> where T : class
{
    private readonly int _capacity;
    private readonly T[] _activeBuffer;
    private readonly bool _resetBuffer;
    private int _sequence; // 0 = запись разрешена, 1 = снимок выполняется

    public MarketDataSnapshotManager(IOptions<MarketDataOptions> options)
    {
        _capacity = options.Value.SnapshotCapacity;
        _resetBuffer = options.Value.ResetBufferAfterPublish;
        // TODO: Заменить на словарь?
        _activeBuffer = new T[_capacity]; // It is ok for singletone
    }

    public int Capacity => _capacity;

    /// <summary>
    /// Запись данных в пул. 
    /// ⚠️ Контракт: каждый вызывающий поток работает со своим поддиапазоном индексов.
    /// Пересечений по индексам между потоками нет. Синхронизация на уровне индексов не требуется.
    /// </summary>
    public void WriteData(int index, T value)
    {
        if (index < 0 || index >= _capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be within [0, Capacity).");
        }

        // Ждём окончания предыдущего снимка с таймаутом 30 секунд
        if (!SpinWait.SpinUntil(() => Volatile.Read(ref _sequence) == 0, TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Snapshot timeout - no data will be written for this interval.");
        }

        // Безопасно: гарантировано отсутствие race condition на index
        _activeBuffer[index] = value;
    }

    public void TakeSnapshot(MarketDataSnapshot<T> snapshot)
    {
        var previous = Interlocked.Exchange(ref _sequence, 1);
        if (previous != 0)
        {
            throw new InvalidOperationException("Nested snapshots are not supported.");
        }

        try
        {
            snapshot.CopyFrom(_activeBuffer.AsSpan(0, _capacity));

            if (_resetBuffer)
            {
                _activeBuffer.AsSpan(0, _capacity).Clear();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _sequence, 0);
        }
    }
}
