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

public static class SignalParamKeys
{
    public const string PriceX = nameof(PriceX);
    public const string PriceY = nameof(PriceY);
    public const string Beta = nameof(Beta);
    public const string Spread = nameof(Spread);
    public const string ZScore = nameof(ZScore);
    public const string ZScoreBuy = nameof(ZScoreBuy);
    public const string ZScoreSell = nameof(ZScoreSell);
    public const string HalfLifeSeconds = nameof(HalfLifeSeconds);
    public const string IsCointegrated = nameof(IsCointegrated);
}