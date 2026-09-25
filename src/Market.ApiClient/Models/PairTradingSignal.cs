namespace Market.ApiClient.Models;

public readonly struct PairTradingSignal
{
    public double Beta { get; init; }
    public double ZScoreBuy { get; init; }
    public double ZScoreSell { get; init; }
    public double VwapAskX { get; init; }
    public double VwapBidX { get; init; }
    public double VwapAskY { get; init; }
    public double VwapBidY { get; init; }
    public double VolumeX { get; init; }
    public double VolumeY { get; init; }
    public string Signal { get; init; }
}