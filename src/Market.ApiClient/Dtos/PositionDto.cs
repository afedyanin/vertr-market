namespace Market.ApiClient.Dtos;

public record class PositionDto
{
    public ushort AssetId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Cash { get; set; }
}
