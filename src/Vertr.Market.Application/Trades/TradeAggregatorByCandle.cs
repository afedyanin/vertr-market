using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;

namespace Vertr.Market.Application.Trades;

public sealed class TradeAggregatorByCandle : IEventHandler<MarketTradeEvent>
{
    private static readonly TimeSpan CandleInterval = Consts.CandlePublishInterval;
    private readonly Dictionary<int, Candle> _activeCandles;

#pragma warning disable CA1805 // Do not initialize unnecessarily
    // Используем Ticks для максимальной производительности сравнений
    private long _maxSeenTradeTicks = 0;
#pragma warning restore CA1805 // Do not initialize unnecessarily

    public TradeAggregatorByCandle(int capacity = 1024)
    {
        _activeCandles = new(capacity);
    }

    public void OnEvent(MarketTradeEvent data, long sequence, bool endOfBatch)
    {
        switch (data.Type)
        {
            case MarketTradeEventType.Trade:
                ProcessTrade(data.Trade);
                break;

            case MarketTradeEventType.TimerTick:
                var referenceTicks = data.TimerTimestamp.Ticks > _maxSeenTradeTicks ? data.TimerTimestamp.Ticks : _maxSeenTradeTicks;
                var openCandleTicks = referenceTicks - (referenceTicks % CandleInterval.Ticks);
                FlushExpiredCandles(openCandleTicks);
                break;
        }
    }

    private void ProcessTrade(in Trade trade)
    {
        var tradeTicks = trade.Timestamp.Ticks;
        if (tradeTicks > _maxSeenTradeTicks)
        {
            _maxSeenTradeTicks = tradeTicks;
        }

        var tradeOpenTicks = tradeTicks - (tradeTicks % CandleInterval.Ticks);
        var openTime = new DateTime(tradeOpenTicks, trade.Timestamp.Kind);
        ref var candle = ref CollectionsMarshal.GetValueRefOrAddDefault(_activeCandles, trade.AssetId, out var exists);

        if (!exists || !candle.IsInitialized)
        {
            InitCandle(ref candle, in trade, openTime);
            return;
        }

        if (candle.OpenTime.Ticks != tradeOpenTicks)
        {
            _publisher.Publish(in candle);
            InitCandle(ref candle, in trade, openTime);
            return;
        }

        UpdateCandle(ref candle, in trade);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InitCandle(ref Candle candle, in Trade trade, DateTime openTime)
    {
        candle.AssetId = trade.AssetId;
        candle.OpenTime = openTime;
        candle.Open = trade.Price;
        candle.High = trade.Price;
        candle.Low = trade.Price;
        candle.Close = trade.Price;
        candle.Volume = trade.Volume;
        candle.Value = trade.Price * trade.Volume;
        candle.IsInitialized = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateCandle(ref Candle candle, in Trade trade)
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
        candle.Value += (trade.Price * trade.Volume);
    }

    private void FlushExpiredCandles(long currentIntervalStartTicks)
    {
        if (_activeCandles.Count == 0)
        {
            return;
        }

        foreach (var key in _activeCandles.Keys)
        {
            ref var candle = ref CollectionsMarshal.GetValueRefOrNullRef(_activeCandles, key);

            if (Unsafe.IsNullRef(ref candle))
            {
                continue;
            }

            if (candle.IsInitialized && candle.OpenTime.Ticks < currentIntervalStartTicks)
            {
                _publisher.Publish(in candle);
                candle.IsInitialized = false;
            }
        }
    }
}
