using System.Runtime.CompilerServices;
using Disruptor;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.EventHandlers;

public sealed class OrderBookProcessor : IEventHandler<OrderBookEvent>
{
    private readonly int _processorId;
    private readonly int _totalProcessors;

    private readonly DateTime[] _lastTimestamps;
    private readonly int _maxAssetId;

    private bool _hasPendingBatchData;

    public OrderBookProcessor(int processorId, int totalProcessors, int maxAssetId = 65536)
    {
        if (processorId < 0 || processorId >= totalProcessors)
        {
            throw new ArgumentOutOfRangeException(nameof(processorId), "ID должен быть от 0 до total-1");
        }

        _processorId = processorId;
        _totalProcessors = totalProcessors;
        _maxAssetId = maxAssetId;
        _lastTimestamps = new DateTime[maxAssetId];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnEvent(OrderBookEvent data, long sequence, bool endOfBatch)
    {
        if ((data.OrderBook.AssetId % _totalProcessors) == _processorId)
        {
            _hasPendingBatchData = true;
            ProcessOrderBook(data.OrderBook);
        }

        if (endOfBatch)
        {
            if (_hasPendingBatchData)
            {
                ExecuteBatchFlush();
                _hasPendingBatchData = false;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessOrderBook(in OrderBook book)
    {
        if ((uint)book.AssetId >= (uint)_maxAssetId)
        {
            ThrowAssetIdOutOfRangeException(book.AssetId);
        }

        var lastTs = _lastTimestamps[book.AssetId];

        if (book.Timestamp <= lastTs)
        {
            // Пропускаем устаревший стакан
            return;
        }

        // Обновляем значение по ссылке напрямую в памяти
        _lastTimestamps[book.AssetId] = book.Timestamp;

        // Бизнес-логика
        if (book.BidCount > 0 && book.AskCount > 0)
        {
            ref readonly var bestBid = ref book.Bids[0];
            ref readonly var bestAsk = ref book.Asks[0];

#pragma warning disable IDE0059 // Unnecessary assignment of a value
            var spread = bestAsk.Price - bestBid.Price;
#pragma warning restore IDE0059 // Unnecessary assignment of a value
            // _matchingEngine.UpdateOrderBook(book.AssetId, in book);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // Выносим редкое исключение из горячего пути
    private static void ThrowAssetIdOutOfRangeException(int assetId) =>
        throw new ArgumentOutOfRangeException(nameof(assetId), $"AssetId {assetId} превышает максимальный размер массива.");

    private void ExecuteBatchFlush()
    {
        // Логика тяжелого коммита
    }
}
