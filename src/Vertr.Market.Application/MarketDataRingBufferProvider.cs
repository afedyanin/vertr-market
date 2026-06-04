using Disruptor;
using Disruptor.Dsl;
using Microsoft.Extensions.Options;
using Vertr.Market.Application.Abstractions;

namespace Vertr.Market.Application;

internal class MarketDataRingBufferProvider : IRingBufferProvider<MarketDataSnapshot>, IDisposable
{
    private readonly Disruptor<MarketDataSnapshot> _disruptor;
    private volatile bool _disposed;

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

internal class MarketDataRingBufferProvider<T> : IRingBufferProvider<MarketDataSnapshot<T>>, IDisposable where T : class
{
    private readonly Disruptor<MarketDataSnapshot<T>> _disruptor;
    private volatile bool _disposed;

    public RingBuffer<MarketDataSnapshot<T>> RingBuffer { get; init; }

    public MarketDataRingBufferProvider(
        IEnumerable<IEventHandler<MarketDataSnapshot<T>>> handlers,
        IOptions<MarketDataOptions> options)
    {
        var settings = options.Value;

        _disruptor = new Disruptor<MarketDataSnapshot<T>>(
            eventFactory: () => new MarketDataSnapshot<T>(settings.SnapshotCapacity),
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
