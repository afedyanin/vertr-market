using MemoryPack;

namespace Market.ApiClient.Tcp.Dtos;

[MemoryPackable]
public partial class EmptyDto
{
    public byte Unit { get; set; }
}
