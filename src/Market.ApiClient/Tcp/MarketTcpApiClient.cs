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

    public async Task<TradeTickDto[]> GetTrades(int assetId, int count = 1)
    {
        var requestDto = new GetTradesRequestDto
        {
            AssetId = (ushort)assetId,
            Count = count
        };

        byte[] responseBytes = await _tcpClientConnection.SendRequestAsync(CommandType.GetTradesRequest, requestDto);
        return MemoryPackSerializer.Deserialize<TradeTickDto[]>(responseBytes) ?? [];
    }

    public async Task PostTrades(TradeTickDto[] trades)
    {
        await _tcpClientConnection.SendRequestAsync(CommandType.PostTrades, trades);
    }

    public async Task DeleteTradesByAsset(int assetId)
    {
        var requestDto = new DeleteTradesRequestDto
        {
            AssetId = (ushort)assetId,
        };

        await _tcpClientConnection.SendRequestAsync(CommandType.DeleteTradesByAsset, requestDto);
    }

    public async Task ClearTrades()
    {
        await _tcpClientConnection.SendRequestAsync(CommandType.ClearTrades, new EmptyDto());
    }
}
