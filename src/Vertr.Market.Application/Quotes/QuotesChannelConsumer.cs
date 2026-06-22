using System.Threading.Channels;
using Disruptor;

namespace Vertr.Market.Application.Quotes;

public sealed class QuotesChannelConsumer
{
    private readonly RingBuffer<QuoteEvent> _ringBuffer;
    private readonly ChannelReader<Quote> _reader;
    private readonly TimeSpan _interval;

    public QuotesChannelConsumer(
        RingBuffer<QuoteEvent> ringBuffer,
        Channel<Quote> reader,
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
            await foreach (var quote in _reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                var sequence = _ringBuffer.Next();
                try
                {
                    var eventSlot = _ringBuffer[sequence];
                    eventSlot.Type = QuoteEventType.Quote;
                    eventSlot.Quote = quote;
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
                    eventSlot.Type = QuoteEventType.TimerTick;
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
