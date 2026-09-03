using Market.ApiClient.Dtos;
using Refit;

namespace Market.ApiClient;

public interface IMarketRestApiClient
{
    [Post("/api/order-books")]
    public Task PostBooks([Body] MarketDepthDto[] books);

    [Get("/api/order-books/{assetId}")]
    public Task<MarketDepthDto[]> GetBooks(int assetId, int count = 1);

    [Delete("/api/order-books/{assetId}")]
    public Task DeleteBooksByAsset(int assetId);

    [Delete("/api/order-books")]
    public Task ClearBooks();

    [Get("/api/order-books/stats")]
    public Task<StoreStatsDto> GetBooksStats();

    [Post("/api/trades")]
    public Task PostTrades([Body] TradeTickDto[] trades);

    [Get("/api/trades/{assetId}")]
    public Task<TradeTickDto[]> GetTrades(int assetId, int count = 1);

    [Delete("/api/trades/{assetId}")]
    public Task DeleteTradesByAsset(int assetId);

    [Delete("/api/trades")]
    public Task ClearTrades();

    [Get("/api/trades/stats")]
    public Task<StoreStatsDto> GetTradesStats();
}
