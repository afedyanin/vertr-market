namespace Vertr.Market.Application.Models;


public class Trade
{
    public int InstrumentId { get; set; }
    public decimal Price { get; set; }
    public long Quantity { get; set; }
}