using Market.ApiClient.Dtos;
using Market.Core.Abstractions;
using Market.Core.Models;
using Market.Core.Converters;
using Microsoft.AspNetCore.Mvc;

namespace Market.Host.Controllers;

[Route("api/trades")]
[ApiController]
public class TradesController : ControllerBase
{
    private readonly IObjectStore<TradeTick> _store;

    public TradesController(IObjectStore<TradeTick> store)
    {
        _store = store;
    }

    [HttpPost]
    public Task PostTrades([FromBody] TradeTickDto[] trades)
    {
        _store.Set([.. trades.FromDto()]);
        return Task.FromResult(Ok());
    }

    [HttpGet("{assetId:int}")]
    public Task<TradeTickDto[]> GetTrades(int assetId, int count = 1)
    {
        var items = _store.Get((ushort)assetId, count).ToDto().ToArray();
        return Task.FromResult(items);
    }

    [HttpDelete("{assetId:int}")]
    public Task DeleteTradesByAsset(int assetId)
    {
        _store.Delete((ushort)assetId);
        return Task.FromResult(Ok());
    }

    [HttpDelete()]
    public Task ClearTrades()
    {
        _store.Clear();
        return Task.FromResult(Ok());
    }

    [HttpGet("stats")]
    public Task<StoreStatsDto> GetTradesStats()
    {
        (var setCount, var getCount, var deleteCount) = _store.GetStatistics();
        var res = new StoreStatsDto(setCount, getCount, deleteCount);
        return Task.FromResult(res);
    }
}
