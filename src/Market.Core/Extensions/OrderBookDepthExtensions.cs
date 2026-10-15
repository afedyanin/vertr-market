using Market.Core.Models;

namespace Market.Core.Extensions;

public static class OrderBookDepthExtensions
{
    /// <summary>
    /// Расчет VWAP для проедания стакана на заданный объем
    /// </summary>
    public static double GetVwap(this OrderBookDepth book, double requiredVolume, bool isBuy)
    {
        var levels = isBuy ? book.Asks : book.Bids; // Если покупаем, бьем в лимиты продавцов (Asks), если продаем — в Bids

        // Пустой стакан или некорректный объем — VWAP не определен (NaN фильтруется вызывающим кодом)
        if (levels.Length == 0 || !double.IsFinite(requiredVolume) || requiredVolume < 0)
        {
            return double.NaN;
        }

        // Нулевой объем — берем лучшую цену
        if (requiredVolume <= 0)
        {
            return levels[0].Price;
        }

        double accumulatedVolume = 0;
        double accumulatedCash = 0;

        foreach (var level in levels)
        {
            double remainingVolume = requiredVolume - accumulatedVolume;
            if (remainingVolume <= 0)
            {
                break;
            }

            double volumeToTake = Math.Min(remainingVolume, level.Size);
            accumulatedCash += volumeToTake * level.Price;
            accumulatedVolume += volumeToTake;
        }

        // Если в стакане не хватило ликвидности для нашего объема, возвращаем худшую цену или выкидываем ошибку
        if (accumulatedVolume < requiredVolume)
        {
            return levels[^1].Price; // Худшая доступная цена (рыночное проскальзывание)
        }

        return accumulatedCash / requiredVolume;
    }

    public static Quote GetVwapQuote(this OrderBookDepth book, double requiredVolume)
        => new Quote(book.GetVwap(requiredVolume, false), book.GetVwap(requiredVolume, true));
}
