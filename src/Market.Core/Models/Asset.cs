namespace Market.Core.Models;

public record class Asset
{
    public ushort Id { get; init; }
    public string? ExternalId { get; init; }
    public string? Isin { get; init; }
    public required string Ticker { get; init; }
    public required string ClassCode { get; init; }
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
