using MemoryPack;

namespace Market.ApiClient.Tcp.Dtos;


[MemoryPackable]
public partial record GetTradesRequestDto
{
    public ushort AssetId { get; set; }
    public int Count { get; set; }
}
