using System.Buffers;
using System.Runtime.CompilerServices;
using Disruptor;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.EventHandlers;


public sealed class TradeAggregatorByCandle : IEventHandler<TradeEvent>
{
    private readonly TimeSpan _interval;
    private readonly ICandleSnapshotPublisher _publisher;

    private readonly Dictionary<int, Trade> _bufferA;
    private readonly Dictionary<int, Trade> _bufferB;

    private volatile Dictionary<int, Trade> _current;
    private volatile Dictionary<int, Trade> _snapshot;

    public TradeAggregatorByCandle(
        ICandleSnapshotPublisher publisher,
        TimeSpan interval,
        int capacity = 1024)
    {
        _publisher = publisher;
        _interval = interval;

        _bufferA = new Dictionary<int, Trade>(capacity);
        _bufferB = new Dictionary<int, Trade>(capacity);

        _current = _bufferA;
        _snapshot = _bufferB;
    }
    void IEventHandler<TradeEvent>.OnEvent(TradeEvent data, long sequence, bool endOfBatch)
    {
        OnEvent(in data, sequence, endOfBatch);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnEvent(ref readonly TradeEvent data, long sequence, bool endOfBatch)
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

            var rentedArray = ArrayPool<Trade>.Shared.Rent(count);

            try
            {
                _snapshot.Values.CopyTo(rentedArray, 0);
                _snapshot.Clear();
                await _publisher.PublishAsync(new ReadOnlyMemory<Trade>(rentedArray, 0, count), ct);
            }
            finally
            {
                ArrayPool<Trade>.Shared.Return(rentedArray);
            }
        }
    }
}
