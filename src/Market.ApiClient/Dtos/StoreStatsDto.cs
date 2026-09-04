using MemoryPack;

namespace Market.ApiClient.Dtos;

[MemoryPackable]
public partial record StoreStatsDto(long SetCount, long GetCount, long DeleteCount);
