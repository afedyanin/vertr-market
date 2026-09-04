using MemoryPack;

namespace Market.ApiClient.TcpDtos;

[MemoryPackable]
public partial class GetBooksRequestDto
{
    public int AssetId { get; set; }
    public int Count { get; set; }
}