using System.Buffers;
using System.Runtime.CompilerServices;
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

        _bufferA = new Dictionary<int, OrderBook>(capacity);
        _bufferB = new Dictionary<int, OrderBook>(capacity);

        _current = _bufferA;
        _snapshot = _bufferB;
    }
    void IEventHandler<OrderBookEvent>.OnEvent(OrderBookEvent data, long sequence, bool endOfBatch)
    {
        OnEvent(in data, sequence, endOfBatch);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnEvent(ref readonly OrderBookEvent data, long sequence, bool endOfBatch)
    {
        ref readonly var book = ref data.OrderBook;
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

            try
            {
                _snapshot.Values.CopyTo(rentedArray, 0);
                _snapshot.Clear();
                await _publisher.PublishAsync(new ReadOnlyMemory<OrderBook>(rentedArray, 0, count), ct);
            }
            finally
            {
                ArrayPool<OrderBook>.Shared.Return(rentedArray);
            }
        }
    }
}

public interface IOrderBookSnapshotPublisher
{
    Task PublishAsync(ReadOnlyMemory<OrderBook> books, CancellationToken ct);
}
