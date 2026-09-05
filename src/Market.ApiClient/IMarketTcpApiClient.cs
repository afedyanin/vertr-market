using Market.ApiClient.Dtos;

namespace Market.ApiClient;

public interface IMarketTcpApiClient
{
    public Task PostBooks(MarketDepthDto[] books);

    public Task<MarketDepthDto[]> GetBooks(int assetId, int count = 1);

    public Task DeleteBooksByAsset(int assetId);

    public Task ClearBooks();
}
