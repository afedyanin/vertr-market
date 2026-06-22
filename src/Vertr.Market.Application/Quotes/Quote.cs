namespace Vertr.Market.Application.Quotes;


public readonly record struct Quote(
    int AssetId,
    DateTime Timestamp,
    decimal Bid,
    decimal Ask);

public enum QuoteEventType
{
    Quote,
    TimerTick
}

public sealed class QuoteEvent
{
    public QuoteEventType Type { get; set; }
    public Quote Quote { get; set; }
    public DateTime TimerTimestamp { get; set; }
}

public sealed class QuoteAggregatedEvent
{
    public Quote[]? Quotes { get; set; }
    public int Count { get; set; }
}

