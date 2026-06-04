using Disruptor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vertr.Market.Application.Abstractions;

namespace Vertr.Market.Application;

public sealed class MarketDataPeriodicPublisher
{
    private readonly MarketDataSnapshotManager _signalManager;
    private readonly ILogger<MarketDataPeriodicPublisher> _logger;
    private readonly MarketDataOptions _options;
    private readonly RingBuffer<MarketDataSnapshot> _ringBuffer;

    public MarketDataPeriodicPublisher(
        MarketDataSnapshotManager signalManager,
        IRingBufferProvider<MarketDataSnapshot> ringBufferProvider,
        ILogger<MarketDataPeriodicPublisher> logger,
        IOptions<MarketDataOptions> options)
    {
        _signalManager = signalManager;
        _logger = logger;
        _options = options.Value;
        _ringBuffer = ringBufferProvider.RingBuffer;
    }

    public TimeSpan Interval => _options.PublishingInterval;

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketDataPeriodicService starting with interval {Interval}", _options.PublishingInterval);

        using var timer = new PeriodicTimer(_options.PublishingInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var sequence = _ringBuffer.Next();

                    try
                    {
                        _signalManager.TakeSnapshot(_ringBuffer[sequence]);
                    }
                    finally
                    {
                        _ringBuffer.Publish(sequence);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MarketDataPeriodicService failed to capture snapshot");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        _logger.LogInformation("MarketDataPeriodicService stopped");
    }
}

public sealed class MarketDataPeriodicPublisher<T> where T : class
{
    private readonly MarketDataSnapshotManager<T> _signalManager;
    private readonly ILogger<MarketDataPeriodicPublisher<T>> _logger;
    private readonly MarketDataOptions _options;
    private readonly RingBuffer<MarketDataSnapshot<T>> _ringBuffer;

    public MarketDataPeriodicPublisher(
        MarketDataSnapshotManager<T> signalManager,
        IRingBufferProvider<MarketDataSnapshot<T>> ringBufferProvider,
        ILogger<MarketDataPeriodicPublisher<T>> logger,
        IOptions<MarketDataOptions> options)
    {
        _signalManager = signalManager;
        _logger = logger;
        _options = options.Value;
        _ringBuffer = ringBufferProvider.RingBuffer;
    }

    public TimeSpan Interval => _options.PublishingInterval;

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketDataPeriodicService of {Type} starting with interval {Interval}", typeof(T).Name, _options.PublishingInterval);

        using var timer = new PeriodicTimer(_options.PublishingInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var sequence = _ringBuffer.Next();

                    try
                    {
                        _signalManager.TakeSnapshot(_ringBuffer[sequence]);
                    }
                    finally
                    {
                        _ringBuffer.Publish(sequence);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MarketDataPeriodicService of {Type} failed to capture snapshot", typeof(T).Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        _logger.LogInformation("MarketDataPeriodicService of {Type} stopped", typeof(T).Name);
    }
}
