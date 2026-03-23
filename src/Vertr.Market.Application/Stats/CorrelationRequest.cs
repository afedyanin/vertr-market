namespace Vertr.Market.Application.Stats;

public record class CorrelationRequest
{
    public DateTime From { get; init; }

    public DateTime? To { get; init; }

    public int AggregationIntervalSec { get; init; }

    public Guid BaseAsset { get; init; }

    public Guid[] DerivedAssets { get; init; } = [];

    public int SigmaCount { get; init; }

    // TODO: Передавать функции для аггрегации и фильтрации в параметрах
}
