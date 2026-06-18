namespace Vertr.Market.Tinvest;

public class TinvestMarketDataSettings
{
    public bool IsEnabled { get; set; }

    public Dictionary<string, int> Assets { get; set; } = [];
}
