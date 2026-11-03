
namespace Market.Core.Models;

public record MyTrade
{
    public int? StrategyId { get; set; }
    public string? OrderId { get; init; }
    public string? TradeId { get; init; }
    public ushort AssetId { get; init; }
    public DateTime Timestamp { get; init; }
    public decimal Qty { get; init; }
    public decimal Price { get; init; }
}
