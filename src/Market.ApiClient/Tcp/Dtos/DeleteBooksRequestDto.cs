using MemoryPack;

namespace Market.ApiClient.Tcp.Dtos;

[MemoryPackable]
public partial class DeleteBooksRequestDto
{
    public ushort AssetId { get; set; }
}
