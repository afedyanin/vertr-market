using System.Runtime.CompilerServices;
using Market.Contracts.Models;

namespace Market.Contracts.Extensions;

public static class OrderBookDepthEtensions
{
    // ANSI Escape-коды для управления цветом в консоли (0 аллокаций)
    private const string ResetColor = "\x1b[0m";
    private const string RedColor = "\x1b[31m";   // Для Asks (Продавцы)
    private const string GreenColor = "\x1b[32m"; // Для Bids (Покупатели)
    private const string CyanColor = "\x1b[36m";  // Для мета-информации

    public static string Dump(this OrderBookDepth dto)
    {
        // Выделяем буфер с запасом, так как ANSI-коды занимают дополнительные символы
        var handler = new DefaultInterpolatedStringHandler(literalLength: 1024, formattedCount: 40);

        handler.AppendLiteral($"{CyanColor}=== Market Depth | Asset: {dto.AssetId} ==={ResetColor}\n");

        // 1. Выводим ASKS (Красный цвет)
        handler.AppendLiteral($"{RedColor}--- ASKS (Sells) ---{ResetColor}\n");
        for (var i = dto.Asks.Length - 1; i >= 0; i--)
        {
            ref readonly var level = ref dto.Asks[i];
            if (level.Size > 0 || level.Price > 0)
            {
                // Подсвечиваем только значения цен и объемов, сохраняя структуру
                handler.AppendLiteral($"  [Ask {i}] Price: {RedColor}{level.Price,-10}{ResetColor} | Volume: {RedColor}{level.Size}{ResetColor}");

                // Маркер для лучшего Ask
                if (i == 0)
                {
                    handler.AppendLiteral("  <-- Best Ask");
                }

                handler.AppendLiteral("\n");
            }
        }

        handler.AppendLiteral("--------------------\n");

        // 2. Выводим BIDS (Зеленый цвет)
        handler.AppendLiteral($"{GreenColor}--- BIDS (Buys)  ---{ResetColor}\n");
        for (var i = 0; i < dto.Bids.Length; i++)
        {
            ref readonly var level = ref dto.Bids[i];
            if (level.Size > 0 || level.Price > 0)
            {
                handler.AppendLiteral($"  [Bid {i}] Price: {GreenColor}{level.Price,-10}{ResetColor} | Volume: {GreenColor}{level.Size}{ResetColor}");

                // Маркер для лучшего Bid
                if (i == 0)
                {
                    handler.AppendLiteral("  <-- Best Bid");
                }

                handler.AppendLiteral("\n");
            }
        }

        handler.AppendLiteral($"{CyanColor}======================================================={ResetColor}");

        return handler.ToStringAndClear();
    }
}
