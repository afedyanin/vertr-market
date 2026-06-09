using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;

namespace Vertr.Market.Application.Models;

public sealed class CandleAggregator
{
    // Хранилище незакрытых свечей в памяти по ID актива.
    private readonly Dictionary<int, Candle> _activeCandles = new(1024);
    private readonly RingBuffer<CandleEvent> _ringBuffer;
    private readonly TimeSpan _candleInterval;
    private readonly int _tradeSize = Unsafe.SizeOf<Trade>();

    public CandleAggregator(TimeSpan candleInterval, RingBuffer<CandleEvent> ringBuffer)
    {
        _candleInterval = candleInterval;
        _ringBuffer = ringBuffer;
    }

    /// <summary>
    /// Периодический сброс незакрытых свечей в Disruptor (запускать в фоне).
    /// </summary>
    public async Task StartEmittingAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_candleInterval);

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            Flush();
        }
    }

    /// <summary>
    /// Парсинг входящего бинарного потока трейдов (0 аллокаций в куче).
    /// </summary>
    public async ValueTask ParseTradeStreamAsync(Stream stream, CancellationToken ct)
    {
        var rentArray = ArrayPool<byte>.Shared.Rent(_tradeSize);
        var memoryBuffer = rentArray.AsMemory(0, _tradeSize);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var bytesRead = await stream.ReadAsync(memoryBuffer, ct).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                ref readonly var trade = ref MemoryMarshal.AsRef<Trade>(memoryBuffer.Span);
                ProcessTrade(in trade);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentArray);
        }

        Flush();
    }

    /// <summary>
    /// Основная логика агрегации (Zero-Allocation).
    /// </summary>
    public void ProcessTrade(in Trade trade)
    {
        // 1. Вычисляем время начала текущего интервала свечи
        var ticks = trade.Timestamp.Ticks;
        var intervalTicks = _candleInterval.Ticks;
        var candleOpenTime = new DateTime(ticks - (ticks % intervalTicks), trade.Timestamp.Kind);

        // 2. Получаем ссылку на свечу в куче (внутри базового массива Dictionary)
        ref var candle = ref CollectionsMarshal.GetValueRefOrAddDefault(_activeCandles, trade.AssetId, out var exists);

        // 3. Если свеча уже была, но её время прошло — отправляем её в Disruptor и сбрасываем стейт
        if (exists && candle.OpenTime != candleOpenTime)
        {
            PublishCandleToDisruptor(in candle);
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

    /// <summary>
    /// Публикация закрытой свечи в кольцевой буфер Disruptor
    /// </summary>
    private void PublishCandleToDisruptor(in Candle candle)
    {
        var sequence = _ringBuffer.Next();
        var @event = _ringBuffer[sequence];
        @event.Value = candle;
        _ringBuffer.Publish(sequence);
    }

    /// <summary>
    /// Сброс всех активных свечей (например, при завершении работы)
    /// </summary>
    public void Flush()
    {
        foreach (var key in _activeCandles.Keys)
        {
            ref var candleRef = ref CollectionsMarshal.GetValueRefOrNullRef(_activeCandles, key);
            if (candleRef.IsInitialized)
            {
                PublishCandleToDisruptor(in candleRef);
            }
        }

        _activeCandles.Clear();
    }
}
