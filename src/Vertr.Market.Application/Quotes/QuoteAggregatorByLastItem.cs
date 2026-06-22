using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;
using Disruptor.Dsl;
using Microsoft.Extensions.Logging;

namespace Vertr.Market.Application.Quotes;

public sealed class QuoteAggregatorByLastItem : IEventHandler<QuoteEvent>
{
    private struct QuoteState
    {
        public Quote Quote;
        public bool IsDirty;
    }

    private readonly Dictionary<int, QuoteState> _activeQuotes;

#pragma warning disable CA1805 // Do not initialize unnecessarily
    private long _maxSeenQoutesTicks = 0;
#pragma warning restore CA1805 // Do not initialize unnecessarily

    private readonly Disruptor<QuoteAggregatedEvent> _disruptor;
    private readonly RingBuffer<QuoteAggregatedEvent> _ringBuffer;
    private readonly ILogger<QuoteAggregatorByLastItem> _logger;

    public QuoteAggregatorByLastItem(
        IEnumerable<IEventHandler<QuoteAggregatedEvent>> handlers,
        ILogger<QuoteAggregatorByLastItem> logger,
        int capacity = 1024)
    {
        _activeQuotes = new(capacity);
        _logger = logger;

        _disruptor = new Disruptor<QuoteAggregatedEvent>(
            () => new QuoteAggregatedEvent(),
            ringBufferSize: 2048,
            TaskScheduler.Default,
            ProducerType.Single,
            new BlockingWaitStrategy());

        var handlersArray = handlers.ToArray();

        if (handlersArray.Length == 0)
        {
            _logger.LogWarning("No registered EventHandlers found for {Event}", nameof(QuoteAggregatedEvent));
        }
        else
        {
            _logger.LogInformation("Registering {Count} EventHandlers to Disruptor.", handlersArray.Length);
            _disruptor.HandleEventsWith(handlersArray);
        }

        _ringBuffer = _disruptor.Start();
    }

    public void OnEvent(QuoteEvent data, long sequence, bool endOfBatch)
    {
        switch (data.Type)
        {
            case QuoteEventType.Quote:
                var qouteTicks = data.Quote.Timestamp.Ticks;
                _maxSeenQoutesTicks = qouteTicks > _maxSeenQoutesTicks ? qouteTicks : _maxSeenQoutesTicks;
                ProcessQuote(data.Quote);
                break;

            case QuoteEventType.TimerTick:
                var timerTicks = data.TimerTimestamp.Ticks;
                var referenceTicks = timerTicks > _maxSeenQoutesTicks ? timerTicks : _maxSeenQoutesTicks;
                FlushExpiredQuotes(referenceTicks);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessQuote(in Quote quote)
    {
        ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(_activeQuotes, quote.AssetId, out _);
        state.Quote = quote;
        state.IsDirty = true;
    }

    private void FlushExpiredQuotes(long referenceTicks)
    {
        if (_activeQuotes.Count == 0)
        {
            return;
        }

        foreach (var key in _activeQuotes.Keys)
        {
            ref var state = ref CollectionsMarshal.GetValueRefOrNullRef(_activeQuotes, key);

            if (Unsafe.IsNullRef(ref state))
            {
                continue;
            }

            if (state.IsDirty && state.Quote.Timestamp.Ticks <= referenceTicks)
            {
                Publish(in state.Quote);
                state.IsDirty = false;
            }
        }
    }

    private void Publish(in Quote quote)
    {
        var sequence = _ringBuffer.Next();
        try
        {
            var eventSlot = _ringBuffer[sequence];
            eventSlot.Quote = quote;
        }
        finally
        {
            _ringBuffer.Publish(sequence);
        }
    }
}
