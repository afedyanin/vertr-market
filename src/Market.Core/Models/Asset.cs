namespace Market.Core.Models;

public record class Asset
{
    public ushort Id { get; init; }
    public string? ExternalId { get; init; }
    public string? Isin { get; init; }
    public string Ticker { get; init; } = string.Empty;
    public string ClassCode { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? Currency { get; init; }
    public DateTime? ExpDate { get; init; }
    public decimal LotSize { get; init; }
    public decimal? MinPriceIncrement { get; init; }
    public decimal? MinPriceIncrementAmount { get; init; }
    public decimal? BasicAssetSize { get; init; }
    public string? BasicAsset { get; init; }
    public string? AssetType { get; init; }
    public string? AssetKind { get; init; }
}
