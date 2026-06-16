using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.Tests.Models;

public class OrderBookEventTests
{
    [Test]
    public void Constructor_InitializesCorrectly()
    {
        _ = new OrderBookEvent(64);

        Assert.That(OrderBookEvent.Capacity, Is.EqualTo(1024));
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
    public void Clear_ClearsAllStoredValues()
    {
        var sut = new OrderBookEvent(10);
        sut[0] = new OrderBook { AssetId = 1 };
        sut[1] = new OrderBook { AssetId = 2 };

        sut.Clear();

        Assert.That(sut[0].AssetId, Is.EqualTo(0));
        Assert.That(sut[1].AssetId, Is.EqualTo(0));
    }

    [Test]
    public void Clear_MultipleTimes_DoesNotThrow()
    {
        var sut = new OrderBookEvent(10);
        sut[0] = new OrderBook { AssetId = 1 };

        Assert.DoesNotThrow(() => sut.Clear());
        Assert.DoesNotThrow(() => sut.Clear());
        Assert.DoesNotThrow(() => sut.Clear());
    }

    [Test]
    public void Indexer_OutOfCapacityRange_ThrowsException()
    {
        var sut = new OrderBookEvent(OrderBookEvent.Capacity);

        Assert.Throws<IndexOutOfRangeException>(() =>
        {
            var _ = sut[OrderBookEvent.Capacity];
        });
    }
}
