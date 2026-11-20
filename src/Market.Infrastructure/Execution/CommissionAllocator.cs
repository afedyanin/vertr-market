using Market.Core.Models;

namespace Market.Infrastructure.Execution;

/// <summary>
/// Распределяет комиссию ордера между его сделками пропорционально объёму.
/// </summary>
/// <remarks>
/// T-Invest сообщает комиссию на весь ордер, а <see cref="MyTrade"/>
/// описывает отдельную сделку. Распределение сохраняет общую сумму комиссии:
/// остаток от деления попадает в последнюю сделку.
/// </remarks>
public static class CommissionAllocator
{
    public static MyTrade[] Distribute(IReadOnlyList<MyTrade> trades, decimal totalCommission)
    {
        ArgumentNullException.ThrowIfNull(trades);

        if (totalCommission < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCommission), totalCommission, "Commission cannot be negative.");
        }

        var result = new MyTrade[trades.Count];

        if (trades.Count == 0 || totalCommission == 0m)
        {
            for (var i = 0; i < trades.Count; i++)
            {
                result[i] = trades[i];
            }

            return result;
        }

        var volumes = new decimal[trades.Count];
        var totalVolume = 0m;

        for (var i = 0; i < trades.Count; i++)
        {
            volumes[i] = Math.Abs(trades[i].Qty * trades[i].Price);
            totalVolume += volumes[i];
        }

        if (totalVolume <= 0m)
        {
            // Объём неизвестен — относим всю комиссию к первой сделке.
            for (var i = 0; i < trades.Count; i++)
            {
                result[i] = trades[i];
            }

            result[0] = result[0] with { Commission = totalCommission };
            return result;
        }

        var allocated = 0m;

        for (var i = 0; i < trades.Count; i++)
        {
            var commission = i == trades.Count - 1
                ? totalCommission - allocated
                : totalCommission * volumes[i] / totalVolume;

            result[i] = trades[i] with { Commission = commission };
            allocated += commission;
        }

        return result;
    }
}
