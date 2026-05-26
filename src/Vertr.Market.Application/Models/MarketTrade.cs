namespace Vertr.Market.Application.Models;


public class MarketTrade
{
    public int InstrumentId { get; set; }
    public decimal Price { get; set; }
    public long Quantity { get; set; }
}