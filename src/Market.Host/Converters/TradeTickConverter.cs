using Market.ApiClient.Dtos;
using Market.Core.Models;

namespace Market.Host.Converters;

internal static class TradeTickConverter
{
    public static IEnumerable<TradeTickDto> ToDto(this IEnumerable<TradeTick> tradeTicks)
        => tradeTicks.Select(ToDto);

    public static IEnumerable<TradeTick> FromDto(this IEnumerable<TradeTickDto> dtos)
        => dtos.Select(FromDto);

    public static TradeTickDto ToDto(this TradeTick tradeTick)
        => new TradeTickDto(tradeTick.MicrosecondTimestamp, tradeTick.Price, tradeTick.Volume, tradeTick.AssetId, tradeTick.Side);

    public static TradeTick FromDto(this TradeTickDto dto)
        => new TradeTick(dto.MicrosecondTimestamp, dto.Price, dto.Volume, dto.AssetId, dto.Side);
}
