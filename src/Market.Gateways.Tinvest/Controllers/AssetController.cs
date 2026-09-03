using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Market.Gateways.Tinvest.Controllers;

[Route("api/assets")]
[ApiController]
public class AssetController : ControllerBase
{
    private readonly TinvestSettings _settings;

    public AssetController(IOptions<TinvestSettings> tinvestOptions)
    {
        _settings = tinvestOptions.Value;
    }

    [HttpGet()]
    public Task<SubscriptionRequest[]> GetAssets()
    {
        var items = _settings.Subscriptions;
        return Task.FromResult(items);
    }
}
