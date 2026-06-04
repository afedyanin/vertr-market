using Disruptor;
using Disruptor.Dsl;
using Microsoft.Extensions.Options;
using Vertr.Market.Application.Abstractions;

namespace Vertr.Market.Application;

internal class MarketDataRingBufferProvider : IRingBufferProvider<MarketDataSnapshot>, IDisposable
{
    private readonly Disruptor<MarketDataSnapshot> _disruptor;
    private bool _disposed;

    public RingBuffer<MarketDataSnapshot> RingBuffer { get; init; }

    public MarketDataRingBufferProvider(
        IEnumerable<IEventHandler<MarketDataSnapshot>> handlers,
        IOptions<MarketDataOptions> options)
    {
        var settings = options.Value;

        _disruptor = new Disruptor<MarketDataSnapshot>(
            eventFactory: () => new MarketDataSnapshot(settings.SnapshotCapacity),
            ringBufferSize: settings.RingBufferSize,
            taskScheduler: TaskScheduler.Default,
            waitStrategy: new BusySpinWaitStrategy(),
            producerType: ProducerType.Single);

        _disruptor.HandleEventsWith(handlers.ToArray());

        RingBuffer = _disruptor.Start();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disruptor.Shutdown();
            _disposed = true;
        }
    }
}
