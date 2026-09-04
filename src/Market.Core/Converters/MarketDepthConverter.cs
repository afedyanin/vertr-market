using Market.ApiClient.Dtos;
using Market.Core.Models;

namespace Market.Core.Converters;

public static class MarketDepthConverter
{
    public static IEnumerable<MarketDepthDto> ToDto(this IEnumerable<MarketDepth> books)
        => books.Select(ToDto);

    public static IEnumerable<MarketDepth> FromDto(this IEnumerable<MarketDepthDto> dtos)
        => dtos.Select(FromDto);

    public static MarketDepthDto ToDto(this MarketDepth book)
        => new MarketDepthDto(
            book.AssetId,
            book.MicrosecondTimestamp,
            [.. book.GetBids().ToDto()],
            [.. book.GetAsks().ToDto()]);

    public static MarketDepth FromDto(this MarketDepthDto dto)
    {
        // TODO: Test it
        var bidsBuffer = new DepthBuffer10();
        dto.Bids.FromDto(ref bidsBuffer);

        var asksBuffer = new DepthBuffer10();
        dto.Asks.FromDto(ref asksBuffer);

        return new MarketDepth(dto.AssetId, dto.MicrosecondTimestamp, ref bidsBuffer, ref asksBuffer);
    }

    private static void FromDto(this PriceLevelDto[] dtos, ref DepthBuffer10 buffer)
    {
        var count = Math.Min(dtos.Length, 10);
        for (var i = 0; i < count; i++)
        {
            buffer[i] = dtos[i].FromDto();
        }
    }

    private static IEnumerable<PriceLevelDto> ToDto(this ReadOnlySpan<PriceLevel> levels)
        => levels.ToArray().Select(ToDto);

    private static PriceLevelDto ToDto(this PriceLevel pl)
        => new PriceLevelDto(pl.Price, pl.Volume);

    private static PriceLevel FromDto(this PriceLevelDto dto)
        => new PriceLevel(dto.Price, dto.Volume);
}
