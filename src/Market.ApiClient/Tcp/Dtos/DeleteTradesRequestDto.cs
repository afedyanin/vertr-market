using MemoryPack;

namespace Market.ApiClient.Tcp.Dtos;

[MemoryPackable]
public partial class DeleteTradesRequestDto
{
    public ushort AssetId { get; set; }
}
