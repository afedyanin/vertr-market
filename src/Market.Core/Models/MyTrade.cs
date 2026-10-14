
namespace Market.Core.Models;

public record MyTrade
{
    public required string SourceId { get; init; }
    public ushort AssetId { get; init; }
    public DateTime Timestamp { get; init; }
    public decimal Qty { get; init; }
    public decimal Price { get; init; }
}
