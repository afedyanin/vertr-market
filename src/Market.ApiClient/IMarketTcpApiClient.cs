using Market.ApiClient.Dtos;

namespace Market.ApiClient;

public interface IMarketTcpApiClient
{
    public Task<MarketDepthDto[]> GetBooksAsync(int assetId, int count = 1);
}
