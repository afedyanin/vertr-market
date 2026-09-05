using Market.ApiClient.Dtos;
using Market.ApiClient.Tcp.Dtos;
using MemoryPack;

namespace Market.ApiClient.Tcp;

internal sealed class MarketTcpApiClient : IMarketTcpApiClient
{
    private readonly ITcpClientConnection _tcpClientConnection;

    public MarketTcpApiClient(ITcpClientConnection tcpClientConnection)
    {
        _tcpClientConnection = tcpClientConnection;
    }

    public async Task<MarketDepthDto[]> GetBooks(int assetId, int count = 1)
    {
        var requestDto = new GetBooksRequestDto
        {
            AssetId = (ushort)assetId,
            Count = count
        };

        byte[] responseBytes = await _tcpClientConnection.SendRequestAsync(CommandType.GetBooksRequest, requestDto);
        return MemoryPackSerializer.Deserialize<MarketDepthDto[]>(responseBytes) ?? [];
    }

    public async Task PostBooks(MarketDepthDto[] books)
    {
        await _tcpClientConnection.SendRequestAsync(CommandType.PostBooks, books);
    }

    public async Task DeleteBooksByAsset(int assetId)
    {
        var requestDto = new DeleteBooksRequestDto
        {
            AssetId = (ushort)assetId,
        };

        await _tcpClientConnection.SendRequestAsync(CommandType.DeleteBooksByAsset, requestDto);
    }

    public async Task ClearBooks()
    {
        await _tcpClientConnection.SendRequestAsync(CommandType.ClearBooks, new EmptyDto());
    }
}
