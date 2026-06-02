using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Vertr.Market.Application.Tests;

public class MarketDataPeriodicServiceTests
{
    [Fact]
    public void Options_DefaultInterval_Is100Milliseconds()
    {
        var options = new MarketDataPeriodicServiceOptions();
        Assert.Equal(TimeSpan.FromMilliseconds(100), options.Interval);
    }

    [Fact]
    public void Options_CustomInterval_SetsCorrectValue()
    {
        var customInterval = TimeSpan.FromMilliseconds(250);
        var options = new MarketDataPeriodicServiceOptions { Interval = customInterval };
        Assert.Equal(customInterval, options.Interval);
    }

    [Fact]
    public void Service_CreatesNonNullInstance()
    {
        using var manager = new SignalManager(64);
        var logger = NullLogger<MarketDataPeriodicService>.Instance;
        var options = Options.Create(new MarketDataPeriodicServiceOptions());

        var service = new MarketDataPeriodicService(manager, logger, options);

        Assert.NotNull(service);
    }

    [Fact]
    public void Service_IntervalProperty_ReturnsConfiguredValue()
    {
        using var manager = new SignalManager(64);
        var customInterval = TimeSpan.FromMilliseconds(50);
        var logger = NullLogger<MarketDataPeriodicService>.Instance;
        var options = Options.Create(new MarketDataPeriodicServiceOptions { Interval = customInterval });

        var service = new MarketDataPeriodicService(manager, logger, options);

        Assert.Equal(customInterval, service.Interval);
    }

    [Fact]
    public async Task Service_SnapshotTakenEvent_FiresDuringExecuteAsync()
    {
        using var manager = new SignalManager(64);
        var logger = NullLogger<MarketDataPeriodicService>.Instance;
        var interval = TimeSpan.FromMilliseconds(50);
        var options = Options.Create(new MarketDataPeriodicServiceOptions { Interval = interval });

        var service = new MarketDataPeriodicService(manager, logger, options);

        var eventCount = 0;
        var lockObj = new object();
        var cts = new CancellationTokenSource();
        var delayCts = new CancellationTokenSource();

        void Handler(MarketDataSnapshot snapshot)
        {
            lock (lockObj)
            {
                eventCount++;
                if (eventCount >= 2)
                {
                    _ = cts.CancelAsync();
                }
            }
        }

        service.SnapshotTaken += Handler;

        var task = service.StartAsync(cts.Token);

        // Wait for the service to start and capture at least 2 snapshots
        await Task.Delay(interval * 3, delayCts.Token);

        _ = cts.CancelAsync();
        await task.WaitAsync(TimeSpan.FromSeconds(10), cts.Token);
        Assert.Equal(2, eventCount);
    }

    [Fact]
    public void Manager_TakeSnapshot_RaisesConsistentData()
    {
        using var manager = new SignalManager(64);

        manager.WriteSignal(0, 1.0);
        var snap1 = manager.TakeSnapshot();
        Assert.Equal(1.0, snap1[0]);

        manager.WriteSignal(0, 2.0);
        var snap2 = manager.TakeSnapshot();
        Assert.Equal(2.0, snap2[0]);
    }

    [Fact]
    public void Manager_SnapshotDataConsistentAcrossIntervals()
    {
        using var manager = new SignalManager(64);

        var snapshotDataList = new List<double[]>();
        var lockObj = new object();

        for (var interval = 0; interval < 5; interval++)
        {
            for (var i = 0; i < 64; i++)
            {
                manager.WriteSignal(i, interval * 100.0 + i);
            }

            var snap = manager.TakeSnapshot();

            lock (lockObj)
            {
                var data = new double[snap.TotalCapacity];
                snap.CopyTo(data);
                snapshotDataList.Add(data);
            }

            for (var i = 0; i < 64; i++)
            {
                Assert.Equal(interval * 100.0 + i, snap[i]);
            }
        }

        Assert.Equal(5, snapshotDataList.Count);
    }

    [Fact]
    public void Manager_MultipleSnapshots_ReturnDifferentData()
    {
        using var manager = new SignalManager(64);

        manager.WriteSignal(0, 1.0);
        var snap1 = manager.TakeSnapshot();
        var data1 = new double[snap1.TotalCapacity];
        snap1.CopyTo(data1);

        manager.WriteSignal(0, 2.0);
        var snap2 = manager.TakeSnapshot();
        var data2 = new double[snap2.TotalCapacity];
        snap2.CopyTo(data2);

        Assert.NotEqual(data1[0], data2[0]);
        Assert.Equal(1.0, data1[0]);
        Assert.Equal(2.0, data2[0]);
    }

    [Fact]
    public void Manager_SnapshotDataNotAffectedBySubsequentWrites()
    {
        using var manager = new SignalManager(64);

        manager.WriteSignal(0, 100.0);
        var snap1 = manager.TakeSnapshot();
        var data1 = new double[snap1.TotalCapacity];
        snap1.CopyTo(data1);

        manager.WriteSignal(0, 200.0);
        manager.TakeSnapshot();

        Assert.Equal(100.0, data1[0]);
    }

    [Fact]
    public void Manager_SnapshotDataNotMutatedBySubsequentWrites()
    {
        using var manager = new SignalManager(64);

        var capturedData = new double[64];

        manager.WriteSignal(0, 42.0);
        var snap = manager.TakeSnapshot();
        snap.CopyTo(capturedData);

        manager.WriteSignal(0, 999.0);
        manager.TakeSnapshot();

        Assert.Equal(42.0, capturedData[0]);
    }

    [Fact]
    public void Dispose_ServiceDisposesGracefully()
    {
        using var manager = new SignalManager(64);
        var logger = NullLogger<MarketDataPeriodicService>.Instance;
        var options = Options.Create(new MarketDataPeriodicServiceOptions());

        var service = new MarketDataPeriodicService(manager, logger, options);

        service.Dispose();
        service.Dispose();
    }
}
