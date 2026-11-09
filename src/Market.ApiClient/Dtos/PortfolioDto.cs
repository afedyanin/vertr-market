namespace Market.ApiClient.Dtos;

public record class PortfolioDto
{
    public Guid StrategyId { get; set; }

    public PositionDto[] Positions { get; set; } = [];
}
