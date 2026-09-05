using Market.ApiClient.Dtos;
using Market.ApiClient.Tcp.Dtos;
using Market.ApiClient.Tcp.Internals;
using MemoryPack;

namespace Market.ApiClient.Tcp;

internal sealed class MarketTcpApiClient : IMarketTcpApiClient
{
    private readonly ITcpApiClient _tcpApiClient;

    public MarketTcpApiClient(ITcpApiClient tcpApiClient)
    {
        _tcpApiClient = tcpApiClient;
    }

    public async Task<MarketDepthDto[]> GetBooks(int assetId, int count = 1)
    {
        var requestDto = new GetBooksRequestDto
        {
            AssetId = (ushort)assetId,
            Count = count
        };

        byte[] responseBytes = await _tcpApiClient.SendRequestAsync(CommandType.GetBooksRequest, requestDto);
        return MemoryPackSerializer.Deserialize<MarketDepthDto[]>(responseBytes) ?? [];
    }

    public async Task PostBooks(MarketDepthDto[] books)
    {
        await _tcpApiClient.SendRequestAsync(CommandType.PostBooks, books);
    }

    public async Task DeleteBooksByAsset(int assetId)
    {
        var requestDto = new DeleteBooksRequestDto
        {
            AssetId = (ushort)assetId,
        };

        await _tcpApiClient.SendRequestAsync(CommandType.DeleteBooksByAsset, requestDto);
    }

    public async Task ClearBooks()
    {
        await _tcpApiClient.SendRequestAsync(CommandType.ClearBooks, new EmptyDto());
    }
}
