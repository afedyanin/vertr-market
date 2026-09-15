namespace Market.ApiClient.Dtos;

public partial record TradeTickDto(
    long MicrosecondTimestamp,
    decimal Price,
    uint Volume,
    ushort AssetId,
    byte Side);
