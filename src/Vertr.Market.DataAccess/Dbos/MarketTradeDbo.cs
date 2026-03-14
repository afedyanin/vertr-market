namespace Vertr.Market.DataAccess.Dbos;

internal sealed class MarketTradeDbo
{
    public Guid Id { get; set; }

    public DateTime TimeUtc { get; set; }

    public Guid InstrumentId { get; set; }

    public string? JsonContent { get; set; }
}
