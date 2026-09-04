using MemoryPack;

namespace Market.ApiClient.Dtos;

[MemoryPackable]
public partial record PriceLevelDto(decimal Price, uint Volume);
