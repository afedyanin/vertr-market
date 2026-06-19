using System.Threading.Channels;
using Disruptor;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.Consumers;

public sealed class TradeChannelConsumer
{
    private readonly RingBuffer<MarketTradeEvent> _ringBuffer;
    private readonly ChannelReader<Trade> _reader;
    private static readonly TimeSpan CandleInterval = Consts.CandlePublishInterval;

    public TradeChannelConsumer(
        RingBuffer<MarketTradeEvent> ringBuffer,
        Channel<Trade> reader)
    {
        _ringBuffer = ringBuffer;
        _reader = reader;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var timerTask = StartTimerLoopAsync(CandleInterval, cts.Token);

        try
        {
            await foreach (var trade in _reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                var sequence = _ringBuffer.Next();
                try
                {
                    var eventSlot = _ringBuffer[sequence];
                    eventSlot.Type = MarketTradeEventType.Trade;
                    eventSlot.Trade = trade;
                }
                finally
                {
                    _ringBuffer.Publish(sequence);
                }
            }
        }
        finally
        {
            await cts.CancelAsync();
            try
            {
                await timerTask.ConfigureAwait(false);
            }
            catch { /* ignore */ }
        }
    }

    private async Task StartTimerLoopAsync(TimeSpan interval, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                var sequence = _ringBuffer.Next();
                try
                {
                    var eventSlot = _ringBuffer[sequence];
                    eventSlot.Type = MarketTradeEventType.TimerTick;
                    eventSlot.TimerTimestamp = DateTime.UtcNow;
                }
                finally
                {
                    _ringBuffer.Publish(sequence);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
