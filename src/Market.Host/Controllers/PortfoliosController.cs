using Market.Core.Repositories;
using Market.Host.Converters;
using Microsoft.AspNetCore.Mvc;

namespace Market.Host.Controllers;

[Route("portfolios")]
[ApiController]
public class PortfoliosController : ControllerBase
{
    private readonly IPortfolioRepository _portfolioRepository;

    public PortfoliosController(IPortfolioRepository portfolioRepository)
    {
        _portfolioRepository = portfolioRepository;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var portfolios = _portfolioRepository.Portfolios;
        return Ok(portfolios.ToDto());
    }

    [HttpGet("{strategyId:guid}")]
    public IActionResult GetByStrategyId(Guid strategyId)
    {
        var portfolio = _portfolioRepository.Portfolios.SingleOrDefault(p => p.StrategyId == strategyId);

        if (portfolio is null)
        {
            return NotFound();
        }

        return Ok(portfolio.ToDto());
    }
}
