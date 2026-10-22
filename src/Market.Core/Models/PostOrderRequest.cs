namespace Market.Core.Models;

public record class PostOrderRequest
{
    public Guid RequestId { get; set; }

    // TODO: Replace with AssetId
    public Guid InstrumentId { get; set; }

    public long QtyLots { get; set; }

    // TODO: Цена за лот? Требуется пересчет
    public decimal Price { get; set; }
}
