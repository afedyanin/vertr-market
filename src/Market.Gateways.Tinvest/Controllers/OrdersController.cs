using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;

namespace Market.Gateways.Tinvest.Controllers;

[Route("api/orders")]
[ApiController]
public class OrdersController : ControllerBase
{
    private readonly TinvestSettings _tinvestSettings;
    private readonly InvestApiClient _apiClient;


    public OrdersController(
        IOptions<TinvestSettings> tinvestOptions,
        InvestApiClient apiClient)
    {
        _tinvestSettings = tinvestOptions.Value;
        _apiClient = apiClient;
    }

    [HttpPost]
    public async Task<IActionResult> PostOrder(Core.Models.PostOrderRequest request)
    {
        if (request.QtyLots == 0)
        {
            return BadRequest($"QtyLost must be non zero.");
        }

        if (request.Price <= decimal.Zero)
        {
            return BadRequest($"Price must be positive.");
        }

        var postOrderRequest = new PostOrderRequest
        {
            AccountId = _tinvestSettings.AccountId,
            OrderId = request.RequestId.ToString(),
            InstrumentId = request.InstrumentId.ToString(),
            Quantity = Math.Abs(request.QtyLots),
            Price = request.Price,
            Direction = request.QtyLots > 0 ? OrderDirection.Buy : OrderDirection.Sell,
            OrderType = request.Price == decimal.Zero ? OrderType.Market : OrderType.Limit,
            TimeInForce = TimeInForceType.TimeInForceUnspecified,
            PriceType = PriceType.Unspecified,
            ConfirmMarginTrade = false,
        };

        var response = await _apiClient.Orders.PostOrderAsync(postOrderRequest);

        return Ok(response);
    }
}
