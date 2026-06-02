namespace Vertr.Market.Application;

/// <summary>
/// Configuration for the periodic market data snapshot service.
/// </summary>
public class MarketDataPeriodicServiceOptions
{
    /// <summary>
    /// The interval between snapshot captures. Default is 100 milliseconds.
    /// Must be between 10ms and 10 seconds.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMilliseconds(100);
}
