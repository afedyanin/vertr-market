using MessagePack;

namespace Market.Core.Models;

[MessagePackObject]
public record PairTradingSignal : ITradingSignal
{
    [Key(0)]
    public required string SourceId { get; set; }

    [Key(1)]
    public double Beta { get; set; }

    [Key(2)]
    public double ZScoreBuy { get; set; }

    [Key(3)]
    public double ZScoreSell { get; set; }

    [Key(4)]
    public PairTradingAction Action { get; set; }

    [Key(5)]
    public required OrderBookDepth BookX { get; set; }

    [Key(6)]
    public required OrderBookDepth BookY { get; set; }
}