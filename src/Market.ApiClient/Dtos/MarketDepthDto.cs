using MemoryPack;

namespace Market.ApiClient.Dtos;

[MemoryPackable]

public partial record MarketDepthDto(
    ushort AssetId,
    long MicrosecondTimestamp,
    PriceLevelDto[] Bids,
    PriceLevelDto[] Asks);

