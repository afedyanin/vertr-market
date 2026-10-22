
namespace Market.Core.Models;

public record MyTrade
{
    public string? TradeId { get; init; }
    public string? OrderId { get; init; }
    public string SourceId { get; init; } = string.Empty; // Portfolio grouping
    public ushort AssetId { get; init; }
    public DateTime Timestamp { get; init; }
    public decimal Qty { get; init; }
    public decimal Price { get; init; }
}
