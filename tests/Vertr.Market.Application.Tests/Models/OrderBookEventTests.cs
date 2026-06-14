using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.Tests.Models;

public class OrderBookEventTests
{
    [Test]
    public void Constructor_WithCapacity_InitializesCorrectly()
    {
        const int capacity = 64;
        var sut = new OrderBookEvent(capacity);

        Assert.That(sut.Capacity, Is.EqualTo(capacity));
        Assert.That(sut.Count, Is.EqualTo(0));
    }

    [Test]
    public void Constructor_WithDifferentCapacities_SetsCorrectCapacity()
    {
        var sut1 = new OrderBookEvent(16);
        var sut2 = new OrderBookEvent(128);
        var sut3 = new OrderBookEvent(1024);

        Assert.That(sut1.Capacity, Is.EqualTo(16));
        Assert.That(sut2.Capacity, Is.EqualTo(128));
        Assert.That(sut3.Capacity, Is.EqualTo(1024));
    }

    [Test]
    public void Indexer_SetAndGet_StoresAndRetrievesValue()
    {
        var sut = new OrderBookEvent(10);
        var book = new OrderBook { AssetId = 42, BidCount = 5, AskCount = 3 };

        sut[0] = book;
        sut[5] = book;

        Assert.That(sut[0].AssetId, Is.EqualTo(42));
        Assert.That(sut[5].AssetId, Is.EqualTo(42));
    }

    [Test]
    public void Indexer_SetMultiple_StoresAllValues()
    {
        var sut = new OrderBookEvent(10);

        for (var i = 0; i < 10; i++)
        {
            sut[i] = new OrderBook { AssetId = i * 100 };
        }

        for (var i = 0; i < 10; i++)
        {
            Assert.That(sut[i].AssetId, Is.EqualTo(i * 100));
        }
    }

    [Test]
    public void Clear_WithCountGreaterThanZero_SetsCountToZero()
    {
        var sut = new OrderBookEvent(10);
        sut[0] = new OrderBook { AssetId = 1 };
        sut[1] = new OrderBook { AssetId = 2 };
        sut.Count = 2;

        sut.Clear();

        Assert.That(sut.Count, Is.EqualTo(0));
    }

    [Test]
    public void Clear_WithCountEqualToZero_DoesNotThrow()
    {
        var sut = new OrderBookEvent(10);
        sut.Count = 0;

        Assert.DoesNotThrow(() => sut.Clear());

        Assert.That(sut.Count, Is.EqualTo(0));
    }

    [Test]
    public void Clear_WithCountGreaterThanZero_ClearsStoredValues()
    {
        var sut = new OrderBookEvent(10);
        sut[0] = new OrderBook { AssetId = 1 };
        sut[1] = new OrderBook { AssetId = 2 };
        sut.Count = 2;

        sut.Clear();

        Assert.That(sut[0].AssetId, Is.EqualTo(0));
        Assert.That(sut[1].AssetId, Is.EqualTo(0));
    }

    [Test]
    public void Clear_MultipleTimes_DoesNotThrow()
    {
        var sut = new OrderBookEvent(10);
        sut[0] = new OrderBook { AssetId = 1 };
        sut.Count = 1;

        Assert.DoesNotThrow(() => sut.Clear());
        Assert.DoesNotThrow(() => sut.Clear());
        Assert.DoesNotThrow(() => sut.Clear());

        Assert.That(sut.Count, Is.EqualTo(0));
    }

    [Test]
    public void Capacity_WithSmallCapacity_ReturnsCorrectSize()
    {
        var sut = new OrderBookEvent(4);
        Assert.That(sut.Capacity, Is.EqualTo(4));
    }

    [Test]
    public void Capacity_WithLargeCapacity_ReturnsCorrectSize()
    {
        var sut = new OrderBookEvent(4096);
        Assert.That(sut.Capacity, Is.EqualTo(4096));
    }

    [Test]
    public void Clear_ThenSetCount_UpdatesCountCorrectly()
    {
        var sut = new OrderBookEvent(10);
        sut[0] = new OrderBook { AssetId = 1 };
        sut.Count = 1;

        sut.Clear();
        sut.Count = 5;

        Assert.That(sut.Count, Is.EqualTo(5));
    }

    [Test]
    public void Indexer_OutOfCapacityRange_ThrowsException()
    {
        var sut = new OrderBookEvent(4);

        Assert.Throws<IndexOutOfRangeException>(() =>
        {
            var _ = sut[100];
        });
    }
}
