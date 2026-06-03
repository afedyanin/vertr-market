namespace Vertr.Market.Application.Tests;

public class SignalManagerTests
{
    [Fact]
    public void Constructor_WithValidCapacity_CreatesManagerWithCorrectCapacity()
    {
        const int capacity = 1024;
        var manager = new SignalManager(capacity);

        Assert.Equal(capacity, manager.Capacity);
    }

    [Fact]
    public void Constructor_WithNegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SignalManager(-1));
    }

    [Fact]
    public void Constructor_WithZeroCapacity_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SignalManager(0));
    }

    [Fact]
    public void WriteSignal_AndRead_ReturnsWrittenValue()
    {
        const int capacity = 64;
        var manager = new SignalManager(capacity);

        manager.WriteSignal(10, 42.5);

        var snapshot = new MarketDataSnapshot(capacity);
        manager.TakeSnapshot(snapshot);
        Assert.Equal(42.5, snapshot[10]);
    }

    [Fact]
    public void WriteSignal_MultipleIndices_AllValuesCapturedInSnapshot()
    {
        const int capacity = 64;
        var manager = new SignalManager(capacity);

        for (var i = 0; i < capacity; i++)
        {
            manager.WriteSignal(i, i * 1.5);
        }

        var snapshot = new MarketDataSnapshot(capacity);
        manager.TakeSnapshot(snapshot);

        for (var i = 0; i < capacity; i++)
        {
            Assert.Equal(i * 1.5, snapshot[i]);
        }
    }

    [Fact]
    public void TakeSnapshot_EmptyManager_ReturnsZeros()
    {
        const int capacity = 64;
        var manager = new SignalManager(capacity);

        var snapshot = new MarketDataSnapshot(capacity);
        manager.TakeSnapshot(snapshot);
        Assert.Equal(0.0, snapshot[5]);
    }

    [Fact]
    public void TakeSnapshot_MultipleTimes_ReturnsDifferentData()
    {
        const int capacity = 64;
        var manager = new SignalManager(capacity);

        manager.WriteSignal(0, 1.0);
        var snap1 = new MarketDataSnapshot(capacity);
        manager.TakeSnapshot(snap1);
        Assert.Equal(1.0, snap1[0]);

        manager.WriteSignal(0, 2.0);

        var snap2 = new MarketDataSnapshot(capacity);
        manager.TakeSnapshot(snap2);
        Assert.Equal(2.0, snap2[0]);
        Assert.NotEqual(snap1[0], snap2[0]);
    }

    [Fact]
    public void TakeSnapshot_DuplicateIndexInSameInterval_KeepsLastValue()
    {
        const int capacity = 64;
        var manager = new SignalManager(capacity);

        manager.WriteSignal(5, 10.0);
        manager.WriteSignal(5, 20.0);
        manager.WriteSignal(5, 30.0);

        var snapshot = new MarketDataSnapshot(capacity);
        manager.TakeSnapshot(snapshot);
        Assert.Equal(30.0, snapshot[5]);
    }

    [Fact]
    public void TakeSnapshot_AfterMultipleIntervals_DataConsistentPerInterval()
    {
        const int capacity = 64;
        var manager = new SignalManager(capacity);

        // Interval 1: write values and capture snapshot
        manager.WriteSignal(0, 1.0);
        manager.WriteSignal(1, 1.1);
        var snap1 = new MarketDataSnapshot(capacity);
        manager.TakeSnapshot(snap1);
        Assert.Equal(1.0, snap1[0]);
        Assert.Equal(1.1, snap1[1]);

        // After snapshot, active buffer is automatically cleared by TakeSnapshot()
        manager.WriteSignal(0, 2.0);
        manager.WriteSignal(1, 2.1);
        var snap2 = new MarketDataSnapshot(capacity);
        manager.TakeSnapshot(snap2);
        Assert.Equal(2.0, snap2[0]);
        Assert.Equal(2.1, snap2[1]);

        // Interval 3: verify no stale data leaks into new snapshots
        manager.WriteSignal(0, 3.0);
        manager.WriteSignal(1, 3.1);
        var snap3 = new MarketDataSnapshot(capacity);
        manager.TakeSnapshot(snap3);
        Assert.Equal(3.0, snap3[0]);
        Assert.Equal(3.1, snap3[1]);
    }

    [Fact]
    public void TakeSnapshot_SnapshotDataNotAffectedBySubsequentWrites()
    {
        const int capacity = 64;
        var manager = new SignalManager(capacity);

        manager.WriteSignal(0, 100.0);
        var snapshot = new MarketDataSnapshot(capacity);
        manager.TakeSnapshot(snapshot);

        manager.WriteSignal(0, 200.0);

        Assert.Equal(100.0, snapshot[0]);
    }

    [Fact]
    public void WriteSignal_WithInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        var manager = new SignalManager(64);

        Assert.Throws<ArgumentOutOfRangeException>(() => manager.WriteSignal(-1, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => manager.WriteSignal(64, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => manager.WriteSignal(1000, 1.0));
    }

    [Fact]
    public async Task ConcurrentWrites_DifferentIndices_AllValuesCapturedCorrectly()
    {
        const int capacity = 1024;
        const int writerCount = 8;
        const int writesPerWriter = 128;

        var manager = new SignalManager(capacity);

        var tasks = new List<Task>();
        for (var w = 0; w < writerCount; w++)
        {
            var writerId = w;
            tasks.Add(Task.Run(() =>
            {
                for (var i = 0; i < writesPerWriter; i++)
                {
                    var index = writerId * writesPerWriter + i;
                    if (index < capacity)
                    {
                        manager.WriteSignal(index, writerId * 1000.0 + i);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        var snapshot = new MarketDataSnapshot(capacity);
        manager.TakeSnapshot(snapshot);

        for (var w = 0; w < writerCount; w++)
        {
            for (var i = 0; i < writesPerWriter; i++)
            {
                var index = w * writesPerWriter + i;
                if (index < capacity)
                {
                    Assert.Equal(w * 1000.0 + i, snapshot[index]);
                }
            }
        }
    }

    [Fact]
    public async Task ConcurrentWrites_SameIndex_LastWriteWins()
    {
        const int capacity = 64;
        const int iterations = 1000;

        var manager = new SignalManager(capacity);

        var tasks = new List<Task>();
        for (var t = 0; t < 4; t++)
        {
            var threadId = t;
            tasks.Add(Task.Run(() =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    manager.WriteSignal(5, threadId * 10000.0 + i);
                }
            }));
        }

        await Task.WhenAll(tasks);

        var snapshot = new MarketDataSnapshot(capacity);
        manager.TakeSnapshot(snapshot);

        var expected = 3 * 10000.0 + (iterations - 1);
        Assert.Equal(expected, snapshot[5]);
    }

    [Fact]
    public void SnapshotData_TotalCapacity_ReturnsCorrectCapacity()
    {
        const int capacity = 256;
        var manager = new SignalManager(capacity);

        var snapshot = new MarketDataSnapshot(capacity);
        manager.TakeSnapshot(snapshot);
        Assert.Equal(capacity, snapshot.TotalCapacity);
    }
}
