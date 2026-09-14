using Market.ApiClient.Dtos;

namespace Market.ApiClient;

public interface IMarketTcpApiClient
{
    public Task PostBooks(MarketDepthDto[] books);

    public Task<MarketDepthDto[]> GetBooks(int assetId, int count = 1);

    public Task DeleteBooksByAsset(int assetId);

    public Task ClearBooks();

    public Task PostTrades(TradeTickDto[] trades);

    public Task<TradeTickDto[]> GetTrades(int assetId, int count = 1);

    public Task DeleteTradesByAsset(int assetId);

    public Task ClearTrades();
}
