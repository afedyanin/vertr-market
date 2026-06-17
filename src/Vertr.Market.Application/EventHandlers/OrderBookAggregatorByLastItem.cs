using System.Buffers;
using Disruptor;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.EventHandlers;

public class OrderBookAggregatorByLastItem : IEventHandler<OrderBookEvent>
{
    private readonly TimeSpan _interval;

    private readonly IOrderBookSnapshotPublisher _publisher;

    private readonly Dictionary<int, OrderBook> _bufferA;
    private readonly Dictionary<int, OrderBook> _bufferB;

    private volatile Dictionary<int, OrderBook> _current;
    private volatile Dictionary<int, OrderBook> _snapshot;

    public OrderBookAggregatorByLastItem(
        IOrderBookSnapshotPublisher publisher,
        TimeSpan interval,
        int capacity = 1024)
    {
        _publisher = publisher;
        _interval = interval;

        _bufferA = new(capacity);
        _bufferB = new(capacity);

        _current = _bufferA;
        _snapshot = _bufferB;
    }

    public void OnEvent(OrderBookEvent data, long sequence, bool endOfBatch)
    {
        var book = data.OrderBook;

        if (_current.TryGetValue(book.AssetId, out var oldBook))
        {
            OrderBookPool.Return(oldBook);
        }

        _current[book.AssetId] = book;
    }

    public async Task StartEmittingAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(ct))
        {
            var snapshot = Interlocked.Exchange(ref _current, _snapshot);
            _snapshot = snapshot;

            var count = _snapshot.Count;
            if (count == 0)
            {
                continue;
            }

            var rentedArray = ArrayPool<OrderBook>.Shared.Rent(count);
            _snapshot.Values.CopyTo(rentedArray, 0);
            _snapshot.Clear();
            _ = PublishSnapshotAsync(rentedArray, count, ct);
        }
    }

    private async Task PublishSnapshotAsync(OrderBook[] rentedArray, int count, CancellationToken ct)
    {
        try
        {
            var segment = new ArraySegment<OrderBook>(rentedArray, 0, count);
            await _publisher.PublishAsync(segment, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error publishing snapshot: {ex.Message}");
        }
        finally
        {
            for (var i = 0; i < count; i++)
            {
                OrderBookPool.Return(rentedArray[i]);
            }

            ArrayPool<OrderBook>.Shared.Return(rentedArray);
        }
    }
}

public interface IOrderBookSnapshotPublisher
{
    Task PublishAsync(IReadOnlyList<OrderBook> books, CancellationToken ct);
}
