using Disruptor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vertr.Market.Application;

public sealed class MarketDataPeriodicService
{
    private readonly SignalManager _signalManager;
    private readonly ILogger<MarketDataPeriodicService> _logger;
    private readonly MarketDataPeriodicServiceOptions _options;
    private readonly RingBuffer<MarketDataSnapshot> _ringBuffer;

    public MarketDataPeriodicService(
        SignalManager signalManager,
        RingBuffer<MarketDataSnapshot> ringBuffer,
        ILogger<MarketDataPeriodicService> logger,
        IOptions<MarketDataPeriodicServiceOptions> options)
    {
        _signalManager = signalManager;
        _logger = logger;
        _options = options.Value;
        _ringBuffer = ringBuffer;
    }

    public TimeSpan Interval => _options.Interval;

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketDataPeriodicService starting with interval {Interval}", _options.Interval);

        using var timer = new PeriodicTimer(_options.Interval);

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

        _logger.LogInformation("MarketDataPeriodicService stopped");
    }
}
