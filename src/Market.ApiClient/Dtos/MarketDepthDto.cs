namespace Market.ApiClient.Dtos;

public partial record MarketDepthDto(
    ushort AssetId,
    long MicrosecondTimestamp,
    PriceLevelDto[] Bids,
    PriceLevelDto[] Asks);

