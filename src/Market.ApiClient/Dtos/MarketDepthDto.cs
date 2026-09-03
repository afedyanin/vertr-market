namespace Market.ApiClient.Dtos;

public record class MarketDepthDto(
    ushort AssetId,
    long MicrosecondTimestamp,
    PriceLevelDto[] Bids,
    PriceLevelDto[] Asks);

