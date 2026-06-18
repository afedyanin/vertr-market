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

    private readonly TimeSpan _interval;
    private readonly IOrderBookSnapshotPublisher _publisher;

    private readonly Dictionary<int, OrderBookState> _activeBooks;
    private long _maxSeenBookTicks;

    public OrderBookAggregatorByLastItem(
        IOrderBookSnapshotPublisher publisher,
        TimeSpan interval,
        int capacity = 1024)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _interval = interval;
        _activeBooks = new(capacity);
    }

    public void OnEvent(OrderBookEvent data, long sequence, bool endOfBatch)
    {
        switch (data.Type)
        {
            case OrderBookEventType.OrderBook:
                ProcessOrderBook(data.OrderBook);
                break;

            case OrderBookEventType.TimerTick:
                var referenceTicks = data.TimerTimestamp.Ticks > _maxSeenBookTicks
                    ? data.TimerTimestamp.Ticks
                    : _maxSeenBookTicks;

                FlushExpiredBooks(referenceTicks);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessOrderBook(in OrderBook book)
    {
        var bookTicks = book.Timestamp.Ticks;
        if (bookTicks > _maxSeenBookTicks)
        {
            _maxSeenBookTicks = bookTicks;
        }

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

        // Рассчитываем верхнюю границу временного интервала, который подлежит отправке
        var currentIntervalStartTicks = referenceTicks - (referenceTicks % _interval.Ticks);

        foreach (var key in _activeBooks.Keys)
        {
            ref var state = ref CollectionsMarshal.GetValueRefOrNullRef(_activeBooks, key);

            if (Unsafe.IsNullRef(ref state))
            {
                continue;
            }

            // Отправляем стакан, если он обновился И время его формирования строго меньше текущей границы таймера
            if (state.IsDirty && state.Book.Timestamp.Ticks < currentIntervalStartTicks)
            {
                _publisher.Publish(in state.Book);
                state.IsDirty = false; // Сбрасываем флаг изменений, стакан отправлен
            }
        }
    }
}
