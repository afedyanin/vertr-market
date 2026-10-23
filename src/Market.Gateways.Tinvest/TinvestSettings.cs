using Tinkoff.InvestApi;

namespace Market.Gateways.Tinvest;

public class TinvestSettings
{
    public InvestApiSettings? InvestApiSettings { get; set; }

    public bool DataStreamEnabled { get; set; }

    public bool TradesStreamEnabled { get; set; }

    public int OrderBookDepth { get; set; }

    public string[] Subscriptions { get; set; } = [];

    public string OutputDirectory { get; set; } = "tinvest";

    public string StaticDataDirectory { get; set; } = "JsonStaticData";

    public TimeSpan TradeAggergationInterval { get; set; } = TimeSpan.FromSeconds(5);

    public string AccountId { get; set; } = string.Empty;
}
