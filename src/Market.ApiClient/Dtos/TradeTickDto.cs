namespace Market.ApiClient.Dtos;

public record class TradeTickDto(
    long MicrosecondTimestamp,
    decimal Price,
    uint Volume,
    ushort AssetId,
    byte Side);
