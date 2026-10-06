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
    public double VwapAskX { get; set; }

    [Key(4)]
    public double VwapBidX { get; set; }

    [Key(5)]
    public double VwapAskY { get; set; }

    [Key(6)]
    public double VwapBidY { get; set; }

    [Key(7)]
    public double VolumeX { get; set; }

    [Key(8)]
    public double VolumeY { get; set; }

    [Key(9)]
    public string Signal { get; set; } = string.Empty;

    [Key(10)]
    public ushort AssetX { get; set; }

    [Key(11)]
    public ushort AssetY { get; set; }
}