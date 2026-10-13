using MessagePack;

namespace Market.Core.Models;

[MessagePackObject]
public record PairTradingSignal : ITradingSignal
{
    [Key(0)]
    public double Beta { get; set; }

    [Key(1)]
    public double ZScoreBuy { get; set; }

    [Key(2)]
    public double ZScoreSell { get; set; }

    [Key(3)]
    public string Signal { get; set; } = string.Empty;

    [Key(4)]
    public required OrderBookDepth BookX { get; set; }

    [Key(5)]
    public required OrderBookDepth BookY { get; set; }
}