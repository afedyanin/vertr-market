using System.Runtime.InteropServices;
using Disruptor;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.EventHandlers;


public sealed class TradeAggregatorByCandle : IEventHandler<TradeEvent>
{
    private readonly TimeSpan _candleInterval;
    private readonly ICandleSnapshotPublisher _publisher;
    private readonly Dictionary<int, Candle> _activeCandles;

    public TradeAggregatorByCandle(
        ICandleSnapshotPublisher publisher,
        TimeSpan candleInterval,
        int capacity = 1024)
    {
        _publisher = publisher;
        _candleInterval = candleInterval;
        _activeCandles = new(capacity);
    }

    public void OnEvent(TradeEvent data, long sequence, bool endOfBatch)
    {
        var trade = data.Trade;

        // 1. Вычисляем время начала текущего интервала свечи
        var ticks = trade.Timestamp.Ticks;
        var intervalTicks = _candleInterval.Ticks;
        var candleOpenTime = new DateTime(ticks - (ticks % intervalTicks), trade.Timestamp.Kind);

        // 2. Получаем ссылку на свечу в куче (внутри базового массива Dictionary)
        ref var candle = ref CollectionsMarshal.GetValueRefOrAddDefault(_activeCandles, trade.AssetId, out var exists);

        // 3. Если свеча уже была, но её время прошло — отправляем её в Publisher и сбрасываем стейт
        if (exists && candle.OpenTime != candleOpenTime)
        {
            _publisher.Publish(in candle);
            exists = false; // Помечаем, что текущую ячейку нужно инициализировать заново
        }

        // 4. Обновляем поля свечи по ссылке (in-place модификация)
        if (!exists)
        {
            candle.AssetId = trade.AssetId;
            candle.OpenTime = candleOpenTime;
            candle.Open = trade.Price;
            candle.High = trade.Price;
            candle.Low = trade.Price;
            candle.Close = trade.Price;
            candle.Volume = trade.Volume;
            candle.IsInitialized = true;
        }
        else
        {
            if (trade.Price > candle.High)
            {
                candle.High = trade.Price;
            }

            if (trade.Price < candle.Low)
            {
                candle.Low = trade.Price;
            }

            candle.Close = trade.Price;
            candle.Volume += trade.Volume;
        }
    }

    public async Task StartEmittingAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_candleInterval);

        while (await timer.WaitForNextTickAsync(ct))
        {
            Flush();
        }
    }

    public void Flush()
    {
        foreach (var key in _activeCandles.Keys)
        {
            ref var candleRef = ref CollectionsMarshal.GetValueRefOrNullRef(_activeCandles, key);
            if (candleRef.IsInitialized)
            {
                _publisher.Publish(in candleRef);
            }
        }

        _activeCandles.Clear();
    }
}

public interface ICandleSnapshotPublisher
{
    void Publish(in Candle candle);
}

