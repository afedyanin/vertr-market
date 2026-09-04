using Market.ApiClient.Dtos;
using Market.Core.Abstractions;
using Market.Core.Models;
using Market.Core.Converters;
using Microsoft.AspNetCore.Mvc;

namespace Market.Host.Controllers;

[Route("api/order-books")]
[ApiController]
public class MarketDepthController : ControllerBase
{
    private readonly IObjectStore<MarketDepth> _store;

    public MarketDepthController(IObjectStore<MarketDepth> store)
    {
        _store = store;
    }

    [HttpPost]
    public Task PostBooks([FromBody] MarketDepthDto[] books)
    {
        _store.Set([.. books.FromDto()]);
        return Task.FromResult(Ok());
    }

    [HttpGet("{assetId:int}")]
    public Task<MarketDepthDto[]> GetBooks(int assetId, int count = 1)
    {
        var items = _store.Get((ushort)assetId, count).ToDto().ToArray();
        return Task.FromResult(items);
    }

    [HttpDelete("{assetId:int}")]
    public Task DeleteBooksByAsset(int assetId)
    {
        _store.Delete((ushort)assetId);
        return Task.FromResult(Ok());
    }

    [HttpDelete()]
    public Task ClearBooks()
    {
        _store.Clear();
        return Task.FromResult(Ok());
    }

    [HttpGet("stats")]
    public Task<StoreStatsDto> GetBooksStats()
    {
        (var setCount, var getCount, var deleteCount) = _store.GetStatistics();
        var res = new StoreStatsDto(setCount, getCount, deleteCount);
        return Task.FromResult(res);
    }
}
