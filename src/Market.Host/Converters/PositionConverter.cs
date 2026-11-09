using Market.ApiClient.Dtos;
using Market.Core.Models;

namespace Market.Host.Converters;

internal static class PositionConverter
{
    public static PositionDto ToDto(this Position position)
        => new PositionDto
        {
            AssetId = position.AssetId,
            Cash = position.Cash,
            Price = position.Price,
            Quantity = position.Quantity,
        };

    public static PositionDto[] ToDto(this IEnumerable<Position> positions)
        => [.. positions.Select(ToDto)];
}
