using System.Collections.Concurrent;
using System.Threading.Channels;
using Market.ApiClient.Dtos;
using Market.Core.Models;

namespace Market.Core;

public class MarketDataAggregator : IDisposable
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _barInterval;
    private readonly ChannelWriter<TimeQuant> _outputWriter;
    private readonly ConcurrentDictionary<ushort, InstrumentState> _instruments = new();

    private readonly CancellationTokenSource _cts = new();
    private Task? _heartbeatTask;

    private sealed class InstrumentState
    {
        public readonly object Lock = new();
        public DateTime NextBarEndTime;
        public decimal LastKnownPrice;
        public decimal? CurrentOpen;
        public decimal CurrentHigh = decimal.MinValue;
        public decimal CurrentLow = decimal.MaxValue;
        public decimal CurrentClose;
        public uint CurrentVolume;
    }

    public MarketDataAggregator(
        TimeProvider timeProvider,
        TimeSpan barInterval,
        ChannelWriter<TimeQuant> outputWriter)
    {
        _timeProvider = timeProvider;
        _barInterval = barInterval;
        _outputWriter = outputWriter;
    }

    public void Start()
    {
        if (_timeProvider == TimeProvider.System)
        {
            _heartbeatTask = Task.Factory.StartNew(
                () => HighResolutionHeartbeatLoop(_cts.Token),
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
    }

    public void ProcessTrade(in TradeTickDto trade)
    {
        var state = _instruments.GetOrAdd(trade.AssetId, _ => InitInstrumentState(_timeProvider.GetUtcNow().UtcDateTime));

        lock (state.Lock)
        {
            // Поддержка бэктеста: если FakeTimeProvider, двигаем время вперед по таймстемпу сделки
            if (_timeProvider is Microsoft.Extensions.Time.Testing.FakeTimeProvider fakeClock
                && trade.Timestamp > fakeClock.GetUtcNow())
            {
                CheckAndEmitBars(trade.AssetId, state, trade.Timestamp);
                fakeClock.SetUtcNow(trade.Timestamp);
            }
            else
            {
                CheckAndEmitBars(trade.AssetId, state, _timeProvider.GetUtcNow().UtcDateTime);
            }

            state.LastKnownPrice = trade.Price;
            state.CurrentOpen ??= trade.Price;

            if (trade.Price > state.CurrentHigh)
            {
                state.CurrentHigh = trade.Price;
            }

            if (trade.Price < state.CurrentLow)
            {
                state.CurrentLow = trade.Price;
            }

            state.CurrentClose = trade.Price;
            state.CurrentVolume += trade.Volume;
        }
    }

    public void ProcessHeartbeat()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var kvp in _instruments)
        {
            var state = kvp.Value;

            if (now >= state.NextBarEndTime)
            {
                lock (state.Lock)
                {
                    CheckAndEmitBars(kvp.Key, state, now);
                }
            }
        }
    }

    private void CheckAndEmitBars(ushort assetId, InstrumentState state, DateTime currentTime)
    {
        while (currentTime >= state.NextBarEndTime)
        {
            TimeQuant finishedBar;
            DateTime barStartTime = state.NextBarEndTime.Subtract(_barInterval);

            if (state.CurrentOpen.HasValue)
            {
                finishedBar = new TimeQuant(
                    assetId,
                    barStartTime,
                    state.CurrentOpen.Value,
                    state.CurrentHigh,
                    state.CurrentLow,
                    state.CurrentClose,
                    state.CurrentVolume
                );

                state.CurrentOpen = null;
                state.CurrentHigh = decimal.MinValue;
                state.CurrentLow = decimal.MaxValue;
                state.CurrentVolume = 0;
            }
            else
            {
                finishedBar = TimeQuant.CreateEmpty(assetId, barStartTime, state.LastKnownPrice);
            }

            _outputWriter.TryWrite(finishedBar);
            state.NextBarEndTime = state.NextBarEndTime.Add(_barInterval);
        }
    }

    private void HighResolutionHeartbeatLoop(CancellationToken ct)
    {
        long ticksPerCheck = TimeSpan.FromMilliseconds(5).Ticks; // Проверка каждые 5 мс гарантирует точность для 10-100 мс баров
        long nextCheckTicks = _timeProvider.GetTimestamp() + ticksPerCheck;

        while (!ct.IsCancellationRequested)
        {
            if (_timeProvider.GetTimestamp() >= nextCheckTicks)
            {
                ProcessHeartbeat();
                nextCheckTicks += ticksPerCheck;
            }

            Thread.SpinWait(50);
        }
    }

    private InstrumentState InitInstrumentState(DateTime now)
        => new InstrumentState
        {
            NextBarEndTime = RoundDown(now, _barInterval).Add(_barInterval)
        };

    private static DateTime RoundDown(DateTime dateTime, TimeSpan interval)
        => new DateTime(dateTime.Ticks - (dateTime.Ticks % interval.Ticks));

    public void Dispose()
    {
        try
        {
            _cts.Cancel();
#pragma warning disable MA0040 // Forward the CancellationToken parameter to methods that take one
            _heartbeatTask?.Wait();
#pragma warning restore MA0040 // Forward the CancellationToken parameter to methods that take one
        }
        catch
        {
        }

        _cts.Dispose();
    }
}