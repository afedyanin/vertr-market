namespace Market.Core.Models;

public record class AlgoMetadata
{
    public static class Keys
    {
        public const string InitialBeta = nameof(InitialBeta);
        public const string InitialErrorCovariance = nameof(InitialErrorCovariance);
        public const string Q = nameof(Q);
        public const string R = nameof(R);
        public const string LotSizeX = nameof(LotSizeX);
        public const string LotSizeY = nameof(LotSizeY);
        public const string EntryThreshold = nameof(EntryThreshold);
        public const string ExitThreshold = nameof(ExitThreshold);
        public const string RequiredYQty = nameof(RequiredYQty);
        public const string FeeRateX = nameof(FeeRateX);
        public const string FeeRateY = nameof(FeeRateY);
        public const string MaxLots = nameof(MaxLots);
    }

    public Guid StrategyId { get; init; }
    public ushort AssetX { get; init; }
    public ushort AssetY { get; init; }
    public Dictionary<string, double> AlgoParams { get; init; } = [];
    public required string AlgoType { get; init; }
}
