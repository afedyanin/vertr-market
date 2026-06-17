using Disruptor;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.EventHandlers;

public class OrderBookAggregatorByLastItem : IEventHandler<OrderBookEvent>
{
    private readonly TimeSpan _interval;

    private readonly IOrderBookSnapshotPublisher _publisher;

    private readonly Dictionary<int, OrderBook> _bufferA;
    private readonly Dictionary<int, OrderBook> _bufferB;

    private Dictionary<int, OrderBook> _current;
    private Dictionary<int, OrderBook> _snapshot;

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
        _current[book.AssetId] = book;
    }

    public async Task StartEmittingAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(ct))
        {
            var snapshot = Interlocked.Exchange(ref _current, _snapshot);
            _snapshot = snapshot;

            var snapshotCopy = new OrderBook[_snapshot.Count];
            _snapshot.Values.CopyTo(snapshotCopy, 0);

            await _publisher.PublishAsync(snapshotCopy, ct);

            _snapshot.Clear();
        }
    }
}

public interface IOrderBookSnapshotPublisher
{
    Task PublishAsync(IReadOnlyList<OrderBook> books, CancellationToken ct);
}
