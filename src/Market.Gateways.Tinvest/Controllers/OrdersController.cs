using Microsoft.AspNetCore.Mvc;

namespace Market.Gateways.Tinvest.Controllers;

[Route("api/orders")]
[ApiController]
public class OrdersController : ControllerBase
{
    private readonly TinvestSettings _tinvestSettings;


    public OrdersController(TinvestSettings tinvestSettings)
    {
        _tinvestSettings = tinvestSettings;
    }

    [HttpPost]
    public async Task<IActionResult> PostOrder()
    {
        return Ok();
    }
}
