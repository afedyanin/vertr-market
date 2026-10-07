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
    public Quote? VwapX { get; set; }

    [Key(4)]
    public Quote? VwapY { get; set; }

    [Key(5)]
    public double VolumeX { get; set; }

    [Key(6)]
    public double VolumeY { get; set; }

    [Key(7)]
    public string Signal { get; set; } = string.Empty;

    [Key(8)]
    public ushort AssetX { get; set; }

    [Key(9)]
    public ushort AssetY { get; set; }
}