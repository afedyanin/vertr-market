using System.Threading.Channels;
using Disruptor;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.Consumers;

public sealed class OrderBookChannelConsumer
{
    private readonly RingBuffer<OrderBookEvent> _ringBuffer;
    private readonly ChannelReader<OrderBook> _reader;
    private readonly TimeSpan _interval;

    public OrderBookChannelConsumer(
        RingBuffer<OrderBookEvent> ringBuffer,
        ChannelReader<OrderBook> reader,
        TimeSpan interval)
    {
        _ringBuffer = ringBuffer;
        _reader = reader;
        _interval = interval;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var timerTask = StartTimerLoopAsync(_interval, cts.Token);

        try
        {
            await foreach (var orderBook in _reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                var sequence = _ringBuffer.Next();
                try
                {
                    var eventSlot = _ringBuffer[sequence];
                    eventSlot.Type = OrderBookEventType.OrderBook;
                    eventSlot.OrderBook = orderBook;
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
                    eventSlot.Type = OrderBookEventType.TimerTick;
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
