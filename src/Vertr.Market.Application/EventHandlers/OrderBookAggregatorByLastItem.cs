using System.Runtime.CompilerServices;
using Disruptor;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.EventHandlers;

internal class OrderBookAggregatorByLastItem : IEventHandler<OrderBookEvent>
{
    private readonly TimeSpan _interval;

    private readonly OrderBook[] _lastBooks;
    private readonly int _maxAssetId;

    public OrderBookAggregatorByLastItem(TimeSpan interval, int maxAssetId = 512)
    {
        _interval = interval;
        _maxAssetId = maxAssetId;
        _lastBooks = new OrderBook[maxAssetId];
    }

    public void OnEvent(OrderBookEvent data, long sequence, bool endOfBatch)
    {
        var book = data.OrderBook;

        if ((uint)book.AssetId >= (uint)_maxAssetId)
        {
            ThrowAssetIdOutOfRangeException(book.AssetId);
        }

        ref var lastBook = ref _lastBooks[book.AssetId];

        if (book.Timestamp <= lastBook.Timestamp)
        {
            // Пропускаем устаревший стакан
            return;
        }

        // Обновляем значение по ссылке напрямую в памяти
        lastBook = book;
    }

    public async Task StartEmittingAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);

        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
            }
        }
        catch (OperationCanceledException) { }
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // Выносим редкое исключение из горячего пути
    private static void ThrowAssetIdOutOfRangeException(int assetId) =>
        throw new ArgumentOutOfRangeException(nameof(assetId), $"AssetId {assetId} превышает максимальный размер массива.");
}
