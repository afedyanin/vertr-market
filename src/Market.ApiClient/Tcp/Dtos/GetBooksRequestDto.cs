using MemoryPack;

namespace Market.ApiClient.Tcp.Dtos;

[MemoryPackable]
public partial record GetBooksRequestDto
{
    public ushort AssetId { get; set; }
    public int Count { get; set; }
}