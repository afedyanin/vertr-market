using Market.Gateways.Tinvest.Helpers;
using Microsoft.AspNetCore.Mvc;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;

namespace Market.Gateways.Tinvest.Controllers;

[Route("api/accounts")]
[ApiController]
public class AccountsController : ControllerBase
{
    private readonly InvestApiClient _apiClient;

    public AccountsController(InvestApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> GetAccounts()
    {
        var response = await _apiClient.Users.GetAccountsAsync();

        if (response is null || response.Accounts is null)
        {
            return NotFound();
        }

        return Ok(response);
    }

    [HttpGet("sandbox")]
    public async Task<IActionResult> GetSandboxAccounts()
    {
        var response = await _apiClient.Sandbox.GetSandboxAccountsAsync(new GetAccountsRequest());

        if (response is null || response.Accounts is null)
        {
            return NotFound();
        }

        return Ok(response);
    }

    [HttpPost("sandbox")]
    public async Task<IActionResult> CreateSandboxAccount(string accountName)
    {
        var tRequest = new OpenSandboxAccountRequest
        {
            Name = accountName,
        };

        var response = await _apiClient.Sandbox.OpenSandboxAccountAsync(tRequest);

        if (response is null)
        {
            return BadRequest();
        }

        return Ok(response);
    }

    [HttpDelete("sandbox/{accountId}")]
    public async Task<IActionResult> CloseSandboxAccount(string accountId)
    {
        var tRequest = new CloseSandboxAccountRequest
        {
            AccountId = accountId,
        };

        _ = await _apiClient.Sandbox.CloseSandboxAccountAsync(tRequest);

        return Ok();
    }

    [HttpPut("sandbox/{accountId}")]
    public async Task<IActionResult> PayIn(string accountId, Money money)
    {
        var tRequest = new SandboxPayInRequest
        {
            AccountId = accountId,
            Amount = money.Convert()
        };

        var response = await _apiClient.Sandbox.SandboxPayInAsync(tRequest);

        if (response is null)
        {
            return BadRequest();
        }

        return Ok(response);
    }

    [HttpGet("portfolio/{accountId}")]
    public async Task<IActionResult> GetPortfolio(string accountId, bool isSandbox = true)
    {
        var request = new PortfolioRequest
        {
            AccountId = accountId,
            Currency = PortfolioRequest.Types.CurrencyRequest.Rub
        };

        var response = isSandbox ? await _apiClient.Sandbox.GetSandboxPortfolioAsync(request) :
            await _apiClient.Operations.GetPortfolioAsync(request);

        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }
}
