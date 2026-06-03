namespace Vertr.Market.Application;

public class MarketDataPeriodicServiceOptions
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromMilliseconds(100);
}
