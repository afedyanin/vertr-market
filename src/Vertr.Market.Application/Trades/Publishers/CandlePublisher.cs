using Disruptor;
using Disruptor.Dsl;
using Microsoft.Extensions.Logging;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.Application.Trades;

namespace Vertr.Market.Application.Trades.Publishers;

internal class CandlePublisher : ICandlePublisher
{
    private readonly Disruptor<CandleEvent> _disruptor;
    private readonly RingBuffer<CandleEvent> _ringBuffer;
    private readonly ILogger _logger;

    public CandlePublisher(
        IEnumerable<IEventHandler<CandleEvent>> handlers,
        ILogger logger)
    {
        _logger = logger;

        _disruptor = new Disruptor<CandleEvent>(
            () => new CandleEvent(),
            ringBufferSize: 2048,
            TaskScheduler.Default,
            ProducerType.Single,
            new BlockingWaitStrategy());

        var handlersArray = handlers.ToArray();

        if (handlersArray.Length == 0)
        {
            _logger.LogWarning("No registered EventHandlers found for {Event}", nameof(CandleEvent));
        }
        else
        {
            _logger.LogInformation("Registering {Count} EventHandlers to Disruptor.", handlersArray.Length);
            _disruptor.HandleEventsWith(handlersArray);
        }

        _ringBuffer = _disruptor.Start();
    }

    public void Publish(in Candle candle)
    {
        var sequence = _ringBuffer.Next();
        try
        {
            var eventSlot = _ringBuffer[sequence];
            eventSlot.Candle = candle;
        }
        finally
        {
            _ringBuffer.Publish(sequence);
        }
    }

    public void Stop()
    {
        _logger.LogInformation("Stopping Disruptor in {ServiceName}...", nameof(CandlePublisher));
        _disruptor.Halt();
    }
}
