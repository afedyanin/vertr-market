using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Market.Gateways.Tinvest.Controllers;

[Route("api/subscriptions")]
[ApiController]
public class SubscriptionsController : ControllerBase
{
    private readonly TinvestSettings _settings;

    public SubscriptionsController(IOptions<TinvestSettings> tinvestOptions)
    {
        _settings = tinvestOptions.Value;
    }

    [HttpGet()]
    public IActionResult GetSubscriptions()
    {
        var subscriptions = _settings.Subscriptions.Where(s => !string.IsNullOrWhiteSpace(s));
        return Ok(subscriptions);
    }
}
