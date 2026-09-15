namespace Market.ApiClient.Dtos;

public partial record MarketDepthDto(
    ushort AssetId,
    DateTime Timestamp,
    PriceLevelDto[] Bids,
    PriceLevelDto[] Asks);

