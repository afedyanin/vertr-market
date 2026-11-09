using Market.ApiClient.Dtos;
using Market.Core.Models;

namespace Market.Host.Converters;

internal static class PortfolioConverter
{
    public static PortfolioDto ToDto(this Portfolio portfolio)
        => new PortfolioDto
        {
            StrategyId = portfolio.StrategyId,
            Positions = portfolio.Positions.ToDto()
        };

    public static PortfolioDto[] ToDto(this IEnumerable<Portfolio> portfolios)
        => [.. portfolios.Select(ToDto)];
}
