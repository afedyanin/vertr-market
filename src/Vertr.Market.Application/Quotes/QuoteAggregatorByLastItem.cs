using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;
using Disruptor.Dsl;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Vertr.Market.Application.Quotes.EventHandlers;

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

    private readonly ObjectPool<Dictionary<int, Quote>> _dictionaryPool;

    public QuoteAggregatorByLastItem(
        IEnumerable<IEventHandler<QuoteAggregatedEvent>> handlers,
        ILogger<QuoteAggregatorByLastItem> logger,
        int capacity = 1024)
    {
        _activeQuotes = new(capacity);
        _logger = logger;

        var provider = new DefaultObjectPoolProvider();
        _dictionaryPool = provider.Create(new DictionaryPoolPolicy(capacity));

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
            _disruptor.HandleEventsWith(new MemoryReleaseHandler(_dictionaryPool));
        }
        else
        {
            _logger.LogInformation("Registering {Count} EventHandlers to Disruptor.", handlersArray.Length);
            _disruptor.HandleEventsWith(handlersArray).Then(new MemoryReleaseHandler(_dictionaryPool));
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

        Dictionary<int, Quote>? batchDictionary = null;

        foreach (var pair in _activeQuotes)
        {
            ref var state = ref CollectionsMarshal.GetValueRefOrNullRef(_activeQuotes, pair.Key);

            if (Unsafe.IsNullRef(ref state) || !state.IsDirty || state.Quote.Timestamp.Ticks > referenceTicks)
            {
                continue;
            }

            batchDictionary ??= _dictionaryPool.Get();
            batchDictionary[pair.Key] = state.Quote;
            state.IsDirty = false;
        }

        if (batchDictionary != null)
        {
            Publish(batchDictionary);
        }
    }

    private void Publish(Dictionary<int, Quote> quotes)
    {
        var sequence = _ringBuffer.Next();
        try
        {
            var eventSlot = _ringBuffer[sequence];
            eventSlot.Quotes = quotes; // Передаем ссылку на арендованный словарь
        }
        finally
        {
            _ringBuffer.Publish(sequence);
        }
    }
}
