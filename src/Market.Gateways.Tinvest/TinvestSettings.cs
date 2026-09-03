using Tinkoff.InvestApi;

namespace Market.Gateways.Tinvest;

internal sealed class TinvestSettings
{
    public InvestApiSettings? InvestApiSettings { get; set; }

    public bool DataStreamEnabled { get; set; }

    public int OrderBookDepth { get; set; }

    public SubscriptionRequest[] Subscriptions { get; set; } = [];

    public string OutputDirectory { get; set; } = "tinvest";
}

internal sealed record class SubscriptionRequest
{
    public ushort AssetId { get; set; }

    public Guid InstrumentId { get; set; }

    public bool Disabled { get; set; }
}