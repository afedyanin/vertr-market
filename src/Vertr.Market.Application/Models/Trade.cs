namespace Vertr.Market.Application.Models;


public record struct Trade
{
    public DateTime TimeUtc;
    public decimal Price;
    public long Qty;
}

