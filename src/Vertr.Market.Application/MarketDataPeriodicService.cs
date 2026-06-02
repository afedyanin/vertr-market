using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vertr.Market.Application;

/// <summary>
/// Background service that periodically captures snapshots of market signal data
/// and raises the SnapshotTaken event with the consistent snapshot data.
///
/// The service uses PeriodicTimer for precise interval-based scheduling and
/// delegates snapshot consistency to SignalManager's ring-buffer double-buffering.
///
/// This service is designed to run as a hosted background service in ASP.NET Core
/// or any IHostedService implementation.
/// </summary>
public sealed class MarketDataPeriodicService : BackgroundService
{
    private readonly SignalManager _signalManager;
    private readonly ILogger<MarketDataPeriodicService> _logger;
    private readonly MarketDataPeriodicServiceOptions _options;

    /// <summary>
    /// Raised on each successful snapshot capture.
    /// The event handler receives the snapshot data.
    /// </summary>
    public Action<MarketDataSnapshot>? SnapshotTaken;

    /// <summary>
    /// Creates a new MarketDataPeriodicService.
    /// </summary>
    /// <param name="signalManager">The shared signal manager for snapshotting.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="options">Configuration options for snapshot interval.</param>
    public MarketDataPeriodicService(
        SignalManager signalManager,
        ILogger<MarketDataPeriodicService> logger,
        IOptions<MarketDataPeriodicServiceOptions> options)
    {
        _signalManager = signalManager;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Gets the configured snapshot interval.
    /// </summary>
    public TimeSpan Interval => _options.Interval;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketDataPeriodicService starting with interval {Interval}", _options.Interval);

        using var timer = new PeriodicTimer(_options.Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = new MarketDataSnapshot(_signalManager.Capacity);
                _signalManager.TakeSnapshot(snapshot);

                _logger.LogDebug(
                    "MarketDataPeriodicService captured snapshot with capacity {Capacity}",
                    snapshot.TotalCapacity);

                SnapshotTaken?.Invoke(snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MarketDataPeriodicService failed to capture snapshot");
            }
        }

        _logger.LogInformation("MarketDataPeriodicService stopped");
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        base.Dispose();
    }
}
