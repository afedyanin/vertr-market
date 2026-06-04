namespace Vertr.Market.Application.Models;

public record class MarketTrade(double Price, long Quantity, BuySell Direction);

public enum BuySell
{
    Buy = 0,
    Sell = 1,
}