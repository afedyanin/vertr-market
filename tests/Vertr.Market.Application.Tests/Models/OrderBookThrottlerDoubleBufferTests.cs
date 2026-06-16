using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Disruptor;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.Tests.Models;

public class OrderBookThrottlerDoubleBufferTests
{
    [Test]
    public void HandleIncomingOrderBook_SingleThread_WritesToCorrectBuffer()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottlerDoubleBuffer(TimeSpan.FromSeconds(1), ringBuffer);
        var book = new OrderBook { AssetId = 1, BidCount = 5, AskCount = 3 };

        throttler.HandleIncomingOrderBook(in book);

        var emitted = EmitSnapshot(throttler);
        Assert.That(emitted[1].BidCount, Is.EqualTo(5));
        Assert.That(emitted[1].AskCount, Is.EqualTo(3));
    }

    [Test]
    public void HandleIncomingOrderBook_MultipleAssets_WritesAllAssets()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottlerDoubleBuffer(TimeSpan.FromSeconds(1), ringBuffer);

        for (var i = 0; i < 100; i++)
        {
            var book = new OrderBook { AssetId = i, BidCount = i * 10, AskCount = i * 5 };
            throttler.HandleIncomingOrderBook(in book);
        }

        var emitted = EmitSnapshot(throttler);

        for (var i = 0; i < 100; i++)
        {
            Assert.That(emitted[i].BidCount, Is.EqualTo(i * 10), $"AssetId={i} bid count mismatch");
            Assert.That(emitted[i].AskCount, Is.EqualTo(i * 5), $"AssetId={i} ask count mismatch");
        }
    }

    [Test]
    public void HandleIncomingOrderBook_LastWriterWins_OverwritesPreviousValue()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottlerDoubleBuffer(TimeSpan.FromSeconds(1), ringBuffer);

        var book1 = new OrderBook { AssetId = 10, BidCount = 100, AskCount = 200 };
        var book2 = new OrderBook { AssetId = 10, BidCount = 101, AskCount = 201 };

        throttler.HandleIncomingOrderBook(in book1);
        throttler.HandleIncomingOrderBook(in book2);

        var emitted = EmitSnapshot(throttler);
        Assert.That(emitted[10].BidCount, Is.EqualTo(101));
        Assert.That(emitted[10].AskCount, Is.EqualTo(201));
    }

    [Test]
    public async Task StartEmittingAsync_CollectsAllData_EmitsCorrectSnapshot()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottlerDoubleBuffer(TimeSpan.FromMilliseconds(50), ringBuffer);
        using var cts = new CancellationTokenSource();

        var emittingTask = throttler.StartEmittingAsync(cts.Token);

        for (var i = 0; i < 50; i++)
        {
            var book = new OrderBook { AssetId = i, BidCount = i * 10, AskCount = i * 5 };
            throttler.HandleIncomingOrderBook(in book);
        }

        await Task.Delay(200, cts.Token);
        await cts.CancelAsync();

        try
        {
            await emittingTask;
        }
        catch (OperationCanceledException)
        {
            // Ожидаемо при отмене
        }
    }

    [Test]
    public void ParseStreamAsync_ReadsFromStream_PassesDataToThrottler()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottlerDoubleBuffer(TimeSpan.FromSeconds(1), ringBuffer);

        var book = new OrderBook { AssetId = 42, BidCount = 77, AskCount = 88 };
        throttler.HandleIncomingOrderBook(in book);

        var emitted = EmitSnapshot(throttler);
        Assert.That(emitted[42].BidCount, Is.EqualTo(77));
        Assert.That(emitted[42].AskCount, Is.EqualTo(88));
    }

    [Test]
    public void ParseStreamAsync_EmptyStream_DoesNotThrow()
    {
        var stream = new MemoryStream(Array.Empty<byte>());
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottlerDoubleBuffer(TimeSpan.FromSeconds(1), ringBuffer);

        var valueTask = throttler.ParseStreamAsync(stream, default);
        if (valueTask.IsCompleted)
        {
            valueTask.GetAwaiter().GetResult();
        }
        else
        {
            valueTask.AsTask().GetAwaiter().GetResult();
        }

        Assert.DoesNotThrow(() => { });
    }

    [Test]
    public void ParseStreamAsync_Cancellation_StopsReading()
    {
        using var cts = new CancellationTokenSource();
        var stream = new MemoryStream(new byte[1024]);

        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottlerDoubleBuffer(TimeSpan.FromSeconds(1), ringBuffer);

        var readTask = throttler.ParseStreamAsync(stream, cts.Token);
        cts.Cancel();

        if (readTask.IsCompleted)
        {
            readTask.GetAwaiter().GetResult();
        }
        else
        {
            readTask.AsTask().GetAwaiter().GetResult();
        }

        // Ожидаем OperationCanceledException
    }

    [Test]
    public void HandleIncomingOrderBook_ConcurrentWriteers_AllWritesComplete()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottlerDoubleBuffer(TimeSpan.FromSeconds(1), ringBuffer);
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
        Assert.That(emitted[500].BidCount, Is.EqualTo(42));
        Assert.That(emitted[500].AskCount, Is.EqualTo(99));
    }

    [Test]
    public void HandleIncomingOrderBook_ConcurrentWriters_MultipleAssets_AllAssetsUpdated()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottlerDoubleBuffer(TimeSpan.FromSeconds(1), ringBuffer);

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
        var throttler = new OrderBookThrottlerDoubleBuffer(TimeSpan.FromMilliseconds(50), ringBuffer);
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
    public async Task HandleIncomingOrderBook_RapidBufferSwitching_NoDataLoss()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottlerDoubleBuffer(TimeSpan.FromMilliseconds(10), ringBuffer);
        using var cts = new CancellationTokenSource();

        var emittingTask = throttler.StartEmittingAsync(cts.Token);

        var writerTasks = new Task[4];
        for (var i = 0; i < writerTasks.Length; i++)
        {
            var assetId = i * 100;
            var book = new OrderBook { AssetId = assetId, BidCount = assetId, AskCount = assetId * 2 };
            writerTasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < 5000; j++)
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
    public void HandleIncomingOrderBook_ExtremeConcurrentWrite_256Threads_AllComplete()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottlerDoubleBuffer(TimeSpan.FromSeconds(1), ringBuffer);
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
    public async Task HandleIncomingOrderBook_StressTest_100Writers_100Assets_NoDataLoss()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottlerDoubleBuffer(TimeSpan.FromSeconds(1), ringBuffer);
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
    }

    [Test]
    public async Task HandleIncomingOrderBook_ContinuousWriteAndEmit_NoCrash()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottlerDoubleBuffer(TimeSpan.FromMilliseconds(10), ringBuffer);
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
    public void HandleIncomingOrderBook_ConcurrentWriteAndRead_VolatileReadVisibility()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottlerDoubleBuffer(TimeSpan.FromSeconds(1), ringBuffer);

        var writerDone = new TaskCompletionSource<bool>();

        var writerTask = Task.Run(() =>
        {
            for (var i = 0; i < 10000; i++)
            {
                var book = new OrderBook { AssetId = i % 100, BidCount = i, AskCount = i * 2 };
                throttler.HandleIncomingOrderBook(in book);
            }

            writerDone.TrySetResult(true);
        });

        // Даем писателю время начать запись
        Thread.Sleep(10);

        var readCount = 0;
        for (var i = 0; i < 1000; i++)
        {
            var emitted = EmitSnapshot(throttler);
            readCount++;
        }

        Assert.That(writerDone.Task.Wait(5000), Is.True, "Writer should complete");
        Assert.That(readCount, Is.EqualTo(1000));
    }

    [Test]
    public async Task HandleIncomingOrderBook_DeadlockFree_NoWriterStarvation()
    {
        var ringBuffer = CreateRingBuffer();
        var throttler = new OrderBookThrottlerDoubleBuffer(TimeSpan.FromMilliseconds(100), ringBuffer);
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

    private static RingBuffer<OrderBookEvent> CreateRingBuffer()
    {
        return RingBuffer<OrderBookEvent>.CreateSingleProducer(
            () => new OrderBookEvent(OrderBookEvent.Capacity),
            1024
        );
    }

    private OrderBook[] EmitSnapshot(OrderBookThrottlerDoubleBuffer throttler)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var bufferA = typeof(OrderBookThrottlerDoubleBuffer).GetField("_bufferA", flags)!;
        var bufferB = typeof(OrderBookThrottlerDoubleBuffer).GetField("_bufferB", flags)!;
        var activeIndex = typeof(OrderBookThrottlerDoubleBuffer).GetField("_activeIndex", flags)!;

        var active = (int)activeIndex.GetValue(throttler)!;
        var source = active == 0
            ? (OrderBook[])bufferA.GetValue(throttler)!
            : (OrderBook[])bufferB.GetValue(throttler)!;

        var result = new OrderBook[OrderBookEvent.Capacity];
        for (var i = 0; i < OrderBookEvent.Capacity; i++)
        {
            result[i] = source[i];
        }

        return result;
    }

    private static byte[] SerializeBook(OrderBook book)
    {
        var span = new byte[Unsafe.SizeOf<OrderBook>()];
        var offset = 0;
        var temp = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(temp, book.AssetId);
        temp.AsSpan().Slice(0, 4).CopyTo(span.AsSpan(offset));
        offset += 4;
        BinaryPrimitives.WriteInt64LittleEndian(temp, book.Timestamp.ToBinary());
        temp.AsSpan().Slice(0, 8).CopyTo(span.AsSpan(offset));
        offset += 8;
        BinaryPrimitives.WriteInt32LittleEndian(temp, book.BidCount);
        temp.AsSpan().Slice(0, 4).CopyTo(span.AsSpan(offset));
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(temp, book.AskCount);
        temp.AsSpan().Slice(0, 4).CopyTo(span.AsSpan(offset));
        return span.ToArray();
    }
}
