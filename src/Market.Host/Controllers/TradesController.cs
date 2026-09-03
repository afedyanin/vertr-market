using Market.ApiClient.Dtos;
using Market.Core.Abstractions;
using Market.Core.Models;
using Market.Host.Converters;
using Microsoft.AspNetCore.Mvc;

namespace Market.Host.Controllers;

[Route("api/trades")]
[ApiController]
public class TradesController : ControllerBase
{
    private readonly IObjectStore<TradeTick> _tickStore;

    public TradesController(IObjectStore<TradeTick> tickStore)
    {
        _tickStore = tickStore;
    }

    [HttpPost]
    public ActionResult Post([FromBody] TradeTickDto[] trades)
    {
        _tickStore.Set([.. trades.FromDto()]);
        return Ok();
    }

    [HttpGet("{assetId:int}")]
    public ActionResult<TradeTickDto[]> Get(int assetId, int count = 1)
    {
        var items = _tickStore.Get((ushort)assetId, count);
        return Ok(items.ToDto());
    }

    [HttpDelete("{assetId:int}")]
    public ActionResult Delete(int assetId)
    {
        _tickStore.Delete((ushort)assetId);
        return Ok();
    }

    [HttpDelete()]
    public ActionResult Clear()
    {
        _tickStore.Clear();
        return Ok();
    }

    [HttpGet("stats")]
    public ActionResult GetStats()
    {
        (var setCount, var getCount, var deleteCount) = _tickStore.GetStatistics();

        return Ok(
            new
            {
                SetCount = setCount,
                GetCount = getCount,
                DeleteCount = deleteCount
            });
    }
}
