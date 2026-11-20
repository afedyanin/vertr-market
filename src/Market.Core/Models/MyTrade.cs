
namespace Market.Core.Models;

public record MyTrade
{
    public Guid? StrategyId { get; set; }
    public string? OrderId { get; init; }
    public string? TradeId { get; init; }
    public ushort AssetId { get; init; }
    public DateTime Timestamp { get; init; }
    public decimal Qty { get; init; }
    public decimal Price { get; init; }

    /// <summary>
    /// Комиссия по сделке в валюте счёта. Величина неотрицательная и
    /// уменьшает денежный поток портфеля.
    /// </summary>
    public decimal Commission { get; init; }
}
