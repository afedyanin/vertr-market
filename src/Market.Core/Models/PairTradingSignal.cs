using MessagePack;

namespace Market.Core.Models;

[MessagePackObject]
public record PairTradingSignal : ITradingSignal
{
    [Key(0)]
    public required string SourceId { get; set; }

    [Key(1)]
    public PairTradingAction Action { get; set; }

    [Key(2)]
    public required OrderBookDepth BookX { get; set; }

    [Key(3)]
    public required OrderBookDepth BookY { get; set; }

    [Key(4)]
    public Dictionary<string, double> Params { get; } = [];
}