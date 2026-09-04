using MemoryPack;

namespace Market.ApiClient.Dtos;

[MemoryPackable]
public partial record TradeTickDto(
    long MicrosecondTimestamp,
    decimal Price,
    uint Volume,
    ushort AssetId,
    byte Side);
