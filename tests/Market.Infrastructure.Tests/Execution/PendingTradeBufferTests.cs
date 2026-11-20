using System.Collections.Concurrent;
using System.Globalization;
using Market.Core.Models;
using Market.Infrastructure.Execution;

namespace Market.Infrastructure.Tests.Execution;

public class PendingTradeBufferTests
{
    private static MyTrade Trade(string orderId, string tradeId) => new()
    {
        OrderId = orderId,
        TradeId = tradeId,
        AssetId = 1,
        Timestamp = DateTime.UtcNow,
        Qty = 1,
        Price = 100,
    };

    [Test]
    public void TryDrain_ReturnsEachTradeExactlyOnce()
    {
        // Arrange
        var buffer = new PendingTradeBuffer(TimeSpan.FromMinutes(1));
        var strategyId = Guid.NewGuid();
        buffer.Add("order-1", [Trade("order-1", "t1"), Trade("order-1", "t2")]);

        // Act
        var first = buffer.TryDrain("order-1", strategyId);
        var second = buffer.TryDrain("order-1", strategyId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(first, Has.Count.EqualTo(2));
            Assert.That(first.Select(t => t.StrategyId), Is.All.EqualTo(strategyId));
            Assert.That(second, Is.Empty);
            Assert.That(buffer.Count, Is.Zero);
        });
    }

    [Test]
    public void Drain_LocalHandleAfterRemoval_DoesNotDuplicateTrades()
    {
        // Arrange: Add вернул handle, но параллельный поток уже снял запись.
        var buffer = new PendingTradeBuffer(TimeSpan.FromMinutes(1));
        var strategyId = Guid.NewGuid();
        var handle = buffer.Add("order-1", [Trade("order-1", "t1")]);

        // Act
        var drainedByResolver = buffer.TryDrain("order-1", strategyId);
        var drainedLocally = buffer.Drain(handle, strategyId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(drainedByResolver, Has.Count.EqualTo(1));
            Assert.That(drainedLocally, Is.Empty);
        });
    }

    [Test]
    public void Drain_LocalHandle_ReturnsTradesWhenEntryWasAlreadyRemoved()
    {
        // Arrange: имитируем окно гонки, когда запись снята до добавления сделки.
        var buffer = new PendingTradeBuffer(TimeSpan.FromMinutes(1));
        var strategyId = Guid.NewGuid();
        var handle = buffer.Add("order-1", []);
        _ = buffer.TryDrain("order-1", strategyId);

        // Add после удаления создаёт новую запись, но handle старой остаётся валидным.
        var lateEntry = buffer.Add("order-1", [Trade("order-1", "t1")]);

        // Act
        var fromNewEntry = buffer.Drain(lateEntry, strategyId);
        var fromOldEntry = buffer.Drain(handle, strategyId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(fromNewEntry, Has.Count.EqualTo(1));
            Assert.That(fromOldEntry, Is.Empty);
        });
    }

    [Test]
    public async Task ConcurrentAddAndDrain_DeliversEachTradeExactlyOnce()
    {
        // Arrange
        const int totalTrades = 5_000;
        var buffer = new PendingTradeBuffer(TimeSpan.FromMinutes(5));
        var strategyId = Guid.NewGuid();
        var delivered = new ConcurrentBag<string>();

        // Producer повторяет поведение сервиса: Add + повторная проверка
        // маппинга + дренаж собственного handle.
        var producer = Task.Run(() =>
        {
            for (var i = 0; i < totalTrades; i++)
            {
                var tradeId = i.ToString(CultureInfo.InvariantCulture);
                var handle = buffer.Add("order-1", [Trade("order-1", tradeId)]);

                foreach (var trade in buffer.Drain(handle, strategyId))
                {
                    delivered.Add(trade.TradeId!);
                }
            }
        });

        // Resolver имитирует PostOrderInternal: снимает запись по ключу.
        var resolver = Task.Run(async () =>
        {
            for (var i = 0; i < 1_000; i++)
            {
                foreach (var trade in buffer.TryDrain("order-1", strategyId))
                {
                    delivered.Add(trade.TradeId!);
                }

                await Task.Yield();
            }
        });

        // Act
        await Task.WhenAll(producer, resolver);

        foreach (var trade in buffer.TryDrain("order-1", strategyId))
        {
            delivered.Add(trade.TradeId!);
        }

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(delivered.Count, Is.EqualTo(totalTrades));
            Assert.That(delivered.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(totalTrades));
        });
    }

    [Test]
    public void PurgeExpired_RemovesOnlyExpiredEntries()
    {
        // Arrange
        var time = new MutableTimeProvider();
        var buffer = new PendingTradeBuffer(TimeSpan.FromMinutes(1), time);

        buffer.Add("old", [Trade("old", "t1")]);

        time.UtcNow = time.UtcNow.AddSeconds(30);
        buffer.Add("fresh", [Trade("fresh", "t2")]);

        time.UtcNow = time.UtcNow.AddSeconds(31);

        // Act
        var dropped = buffer.PurgeExpired();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(dropped, Is.EqualTo(1));
            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer.TryDrain("old", Guid.NewGuid()), Is.Empty);
            Assert.That(buffer.TryDrain("fresh", Guid.NewGuid()), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Constructor_NonPositiveTtl_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new PendingTradeBuffer(TimeSpan.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new PendingTradeBuffer(TimeSpan.FromSeconds(-1)));
        });
    }

    [Test]
    public void Add_EmptyOrderId_Throws()
    {
        var buffer = new PendingTradeBuffer(TimeSpan.FromMinutes(1));

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => buffer.Add(string.Empty, []));
            Assert.Throws<ArgumentException>(() => buffer.Add("  ", []));
        });
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
