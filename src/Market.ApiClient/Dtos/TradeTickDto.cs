namespace Market.ApiClient.Dtos;

public partial record TradeTickDto(
    DateTime Timestamp,
    decimal Price,
    uint Volume,
    ushort AssetId,
    byte Side);
