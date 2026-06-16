using Disruptor;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.Tests.Models;

public class OrderBookThrottlerChangedIndicesTests
{
    [Test]
    public void HandleIncomingOrderBook_SingleAsset_OnlyThatAssetMarkedChanged()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottler(TimeSpan.FromSeconds(1), ringBuffer);
        var book = new OrderBook { AssetId = 42, BidCount = 77, AskCount = 88 };

        throttler.HandleIncomingOrderBook(in book);

        var emitted = EmitSnapshot(throttler);
        Assert.That(emitted[42].BidCount, Is.EqualTo(77), "AssetId=42 should be emitted");
        Assert.That(emitted[42].AskCount, Is.EqualTo(88), "AssetId=42 should be emitted");

        // Остальные должны быть пустыми
        for (var i = 0; i < OrderBookEvent.Capacity; i++)
        {
            if (i != 42)
            {
                Assert.That(emitted[i].AssetId, Is.EqualTo(0), $"AssetId={i} should be empty");
            }
        }
    }

    [Test]
    public void HandleIncomingOrderBook_MultipleAssets_AllMarkedChanged()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottler(TimeSpan.FromSeconds(1), ringBuffer);

        for (var i = 0; i < 50; i++)
        {
            var book = new OrderBook { AssetId = i, BidCount = i * 10, AskCount = i * 5 };
            throttler.HandleIncomingOrderBook(in book);
        }

        var emitted = EmitSnapshot(throttler);

        for (var i = 0; i < 50; i++)
        {
            Assert.That(emitted[i].BidCount, Is.EqualTo(i * 10), $"AssetId={i} bid count");
            Assert.That(emitted[i].AskCount, Is.EqualTo(i * 5), $"AssetId={i} ask count");
        }

        // Остальные пустые
        for (var i = 50; i < OrderBookEvent.Capacity; i++)
        {
            Assert.That(emitted[i].AssetId, Is.EqualTo(0), $"AssetId={i} should be empty");
        }
    }

    [Test]
    public void HandleIncomingOrderBook_SameAssetMultipleTimes_LastWriterWins()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottler(TimeSpan.FromSeconds(1), ringBuffer);

        var book1 = new OrderBook { AssetId = 10, BidCount = 100, AskCount = 200 };
        var book2 = new OrderBook { AssetId = 10, BidCount = 101, AskCount = 201 };
        var book3 = new OrderBook { AssetId = 10, BidCount = 102, AskCount = 202 };

        throttler.HandleIncomingOrderBook(in book1);
        throttler.HandleIncomingOrderBook(in book2);
        throttler.HandleIncomingOrderBook(in book3);

        var emitted = EmitSnapshot(throttler);
        // Должно быть последнее значение
        Assert.That(emitted[10].BidCount, Is.EqualTo(102));
        Assert.That(emitted[10].AskCount, Is.EqualTo(202));
    }

    [Test]
    public async Task StartEmittingAsync_AfterEmit_ChangedFlagsResetForNextTick()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottler(TimeSpan.FromMilliseconds(30), ringBuffer);
        using var cts = new CancellationTokenSource();

        var emittingTask = throttler.StartEmittingAsync(cts.Token);

        // Первая итерация — пишем данные
        var book1 = new OrderBook { AssetId = 5, BidCount = 50, AskCount = 55 };
        throttler.HandleIncomingOrderBook(in book1);

        await Task.Delay(80, cts.Token);

        // Обновляем тот же актив — должен появиться во второй итерации
        var book1Updated = new OrderBook { AssetId = 5, BidCount = 55, AskCount = 60 };
        throttler.HandleIncomingOrderBook(in book1Updated);

        // Вторая итерация — пишем новые данные
        var book2 = new OrderBook { AssetId = 10, BidCount = 100, AskCount = 110 };
        throttler.HandleIncomingOrderBook(in book2);

        await Task.Delay(80, cts.Token);

        await cts.CancelAsync();

        try
        {
            await emittingTask;
        }
        catch (OperationCanceledException)
        {
            // Ожидаемо
        }

        // _latestBooks не очищается — это кэш актуальных данных
        var emitted = EmitSnapshot(throttler);
        Assert.That(emitted[10].BidCount, Is.EqualTo(100), "AssetId=10 should be emitted");
        Assert.That(emitted[5].BidCount, Is.EqualTo(55), "AssetId=5 should still be in cache");
    }

    [Test]
    public void HandleIncomingOrderBook_ConcurrentSameAsset_LastWriterWins()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottler(TimeSpan.FromSeconds(1), ringBuffer);
        var book = new OrderBook { AssetId = 500, BidCount = 42, AskCount = 99 };

        var writerTasks = new Task[16];
        for (var i = 0; i < writerTasks.Length; i++)
        {
            writerTasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < 10000; j++)
                {
                    throttler.HandleIncomingOrderBook(in book);
                }
            });
        }

        Task.WaitAll(writerTasks, TimeSpan.FromSeconds(30));

        var emitted = EmitSnapshot(throttler);
        Assert.That(emitted[500].BidCount, Is.EqualTo(42), "Last writer value");
        Assert.That(emitted[500].AskCount, Is.EqualTo(99), "Last writer value");
    }

    [Test]
    public void HandleIncomingOrderBook_ConcurrentMultipleAssets_AllUpdated()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottler(TimeSpan.FromSeconds(1), ringBuffer);

        var writerTasks = new Task[8];
        for (var assetId = 0; assetId < 8; assetId++)
        {
            var bidCount = assetId * 10;
            var askCount = assetId * 5;
            var book = new OrderBook { AssetId = assetId, BidCount = bidCount, AskCount = askCount };

            writerTasks[assetId] = Task.Run(() =>
            {
                for (var i = 0; i < 5000; i++)
                {
                    throttler.HandleIncomingOrderBook(in book);
                }
            });
        }

        Task.WaitAll(writerTasks, TimeSpan.FromSeconds(30));

        var emitted = EmitSnapshot(throttler);

        for (var assetId = 0; assetId < 8; assetId++)
        {
            Assert.That(emitted[assetId].BidCount, Is.EqualTo(assetId * 10), $"AssetId={assetId} bid count");
            Assert.That(emitted[assetId].AskCount, Is.EqualTo(assetId * 5), $"AssetId={assetId} ask count");
        }
    }

    [Test]
    public async Task HandleIncomingOrderBook_ConcurrentWriterAndReader_NoCorruption()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottler(TimeSpan.FromMilliseconds(50), ringBuffer);
        using var cts = new CancellationTokenSource();

        var emittingTask = throttler.StartEmittingAsync(cts.Token);

        var writerTasks = new Task[16];
        for (var i = 0; i < writerTasks.Length; i++)
        {
            var writerId = i;
            writerTasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < 10000; j++)
                {
                    var book = new OrderBook { AssetId = writerId, BidCount = writerId * 100, AskCount = writerId * 50 };
                    throttler.HandleIncomingOrderBook(in book);
                }
            }, cts.Token);
        }

        await Task.WhenAll(writerTasks);
        await cts.CancelAsync();

        try
        {
            await emittingTask;
        }
        catch (OperationCanceledException)
        {
            // Ожидаемо
        }
    }

    [Test]
    public void HandleIncomingOrderBook_ExtremeConcurrentWrite_NoCrash()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottler(TimeSpan.FromSeconds(1), ringBuffer);
        var book = new OrderBook { AssetId = 999, BidCount = 1, AskCount = 2 };

        var writerTasks = new Task[256];
        for (var i = 0; i < writerTasks.Length; i++)
        {
            writerTasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < 1000; j++)
                {
                    throttler.HandleIncomingOrderBook(in book);
                }
            });
        }

        Task.WaitAll(writerTasks, TimeSpan.FromSeconds(30));

        var emitted = EmitSnapshot(throttler);
        Assert.That(emitted[999].BidCount, Is.EqualTo(1));
        Assert.That(emitted[999].AskCount, Is.EqualTo(2));
    }

    [Test]
    public async Task HandleIncomingOrderBook_StressTest_100Writers_NoDataLoss()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottler(TimeSpan.FromSeconds(1), ringBuffer);
        using var cts = new CancellationTokenSource();

        var emittingTask = throttler.StartEmittingAsync(cts.Token);

        var writerTasks = new Task[100];
        for (var i = 0; i < writerTasks.Length; i++)
        {
            var assetId = i;
            var bidCount = assetId * 10;
            var askCount = assetId * 5;
            var book = new OrderBook { AssetId = assetId, BidCount = bidCount, AskCount = askCount };

            writerTasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < 2000; j++)
                {
                    throttler.HandleIncomingOrderBook(in book);
                }
            }, cts.Token);
        }

        await Task.WhenAll(writerTasks);
        await cts.CancelAsync();

        try
        {
            await emittingTask;
        }
        catch (OperationCanceledException)
        {
            // Ожидаемо
        }

        var emitted = EmitSnapshot(throttler);

        for (var i = 0; i < 100; i++)
        {
            Assert.That(emitted[i].BidCount, Is.EqualTo(i * 10), $"AssetId={i} bid count");
            Assert.That(emitted[i].AskCount, Is.EqualTo(i * 5), $"AssetId={i} ask count");
        }
    }

    [Test]
    public async Task HandleIncomingOrderBook_ContinuousWriteAndEmit_NoCrash()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottler(TimeSpan.FromMilliseconds(10), ringBuffer);
        using var cts = new CancellationTokenSource();

        var emittingTask = throttler.StartEmittingAsync(cts.Token);

        var writerTasks = new Task[8];
        for (var i = 0; i < writerTasks.Length; i++)
        {
            var assetId = i;
            var book = new OrderBook { AssetId = assetId, BidCount = assetId, AskCount = assetId };
            writerTasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < 5000; j++)
                {
                    throttler.HandleIncomingOrderBook(in book);
                }
            }, cts.Token);
        }

        Exception? capturedEx = null;
        try
        {
            Task.WaitAll(writerTasks, TimeSpan.FromSeconds(30));
        }
        catch (Exception ex)
        {
            capturedEx = ex;
        }

        Assert.That(capturedEx, Is.Null, "Writer tasks should not throw");

        await cts.CancelAsync();

        try
        {
            await emittingTask;
        }
        catch (OperationCanceledException)
        {
            // Ожидаемо
        }
    }

    [Test]
    public async Task HandleIncomingOrderBook_DeadlockFree_NoWriterStarvation()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottler(TimeSpan.FromMilliseconds(100), ringBuffer);
        using var cts = new CancellationTokenSource();

        var emittingTask = throttler.StartEmittingAsync(cts.Token);

        var writerTasks = new Task[32];
        for (var i = 0; i < writerTasks.Length; i++)
        {
            var assetId = i % 50;
            var book = new OrderBook { AssetId = assetId, BidCount = assetId, AskCount = assetId };
            writerTasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < 3000; j++)
                {
                    throttler.HandleIncomingOrderBook(in book);
                }
            }, cts.Token);
        }

        await Task.WhenAll(writerTasks);
        await cts.CancelAsync();

        try
        {
            await emittingTask;
        }
        catch (OperationCanceledException)
        {
            // Ожидаемо
        }
    }

    [Test]
    public async Task HandleIncomingOrderBook_RapidFlip_NoStaleDataCopied()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottler(TimeSpan.FromMilliseconds(5), ringBuffer);
        using var cts = new CancellationTokenSource();

        var emittingTask = throttler.StartEmittingAsync(cts.Token);

        var writerTasks = new Task[32];
        for (var i = 0; i < writerTasks.Length; i++)
        {
            var assetId = i;
            var book = new OrderBook { AssetId = assetId, BidCount = assetId, AskCount = assetId * 2 };
            writerTasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < 3000; j++)
                {
                    throttler.HandleIncomingOrderBook(in book);
                }
            }, cts.Token);
        }

        await Task.WhenAll(writerTasks);

        await Task.Delay(200, cts.Token);

        await cts.CancelAsync();

        try
        {
            await emittingTask;
        }
        catch (OperationCanceledException)
        {
            // Ожидаемо
        }

        await Task.Delay(100, CancellationToken.None);

        var emitted = EmitSnapshot(throttler);
        for (var i = 0; i < 32; i++)
        {
            Assert.That(emitted[i].BidCount, Is.EqualTo(i), $"AssetId={i} bid after rapid flip");
            Assert.That(emitted[i].AskCount, Is.EqualTo(i * 2), $"AssetId={i} ask after rapid flip");
        }
    }

    private static RingBuffer<OrderBookEvent> CreateRingBuffer()
    {
        return RingBuffer<OrderBookEvent>.CreateSingleProducer(
            () => new OrderBookEvent(),
            1024
        );
    }

    private OrderBook[] EmitSnapshot(OrderBookThrottler throttler)
    {
        return throttler.GetSnapshot();
    }
}
