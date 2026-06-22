using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.EventHandlers;

public sealed class OrderBookAggregatorByLastItem : IEventHandler<OrderBookEvent>
{
    private struct OrderBookState
    {
        public OrderBook Book;
        public bool IsDirty;
    }

    private readonly IOrderBookSnapshotPublisher _publisher;
    private readonly Dictionary<int, OrderBookState> _activeBooks;

#pragma warning disable CA1805 // Do not initialize unnecessarily
    // Используем Ticks для максимальной производительности сравнений
    private long _maxSeenBookTicks = 0;
#pragma warning restore CA1805 // Do not initialize unnecessarily

    public OrderBookAggregatorByLastItem(
        IOrderBookSnapshotPublisher publisher,
        int capacity = 1024)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _activeBooks = new(capacity);
    }

    public void OnEvent(OrderBookEvent data, long sequence, bool endOfBatch)
    {
        switch (data.Type)
        {
            case OrderBookEventType.OrderBook:
                var bookTicks = data.OrderBook.Timestamp.Ticks;
                _maxSeenBookTicks = bookTicks > _maxSeenBookTicks ? bookTicks : _maxSeenBookTicks;
                ProcessOrderBook(data.OrderBook);
                break;

            case OrderBookEventType.TimerTick:
                var timerTicks = data.TimerTimestamp.Ticks;
                var referenceTicks = timerTicks > _maxSeenBookTicks ? timerTicks : _maxSeenBookTicks;
                FlushExpiredBooks(referenceTicks);
                break;
        }
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessOrderBook(in OrderBook book)
    {
        ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(_activeBooks, book.AssetId, out _);
        state.Book = book;
        state.IsDirty = true;
    }

    private void FlushExpiredBooks(long referenceTicks)
    {
        if (_activeBooks.Count == 0)
        {
            return;
        }

        foreach (var key in _activeBooks.Keys)
        {
            ref var state = ref CollectionsMarshal.GetValueRefOrNullRef(_activeBooks, key);

            if (Unsafe.IsNullRef(ref state))
            {
                continue;
            }

            if (state.IsDirty && state.Book.Timestamp.Ticks <= referenceTicks)
            {
                _publisher.Publish(in state.Book);
                state.IsDirty = false;
            }
        }
    }
}
