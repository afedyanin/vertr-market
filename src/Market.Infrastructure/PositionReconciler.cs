using Market.Core.Models;
using Market.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Market.Infrastructure;

/// <summary>
/// Передаёт фактическую позицию портфеля в стратегию.
/// </summary>
/// <remarks>
/// Поток исполнения применяет сделки к <see cref="Portfolio"/>, после чего
/// вызывает <see cref="TryReconcile"/>. Стратегия получает абсолютные объёмы
/// в лотах и больше не считает позицию по своим намерениям.
/// </remarks>
public sealed class PositionReconciler
{
    private readonly IStrategyRegistry _strategies;
    private readonly ILogger<PositionReconciler> _logger;

    public PositionReconciler(IStrategyRegistry strategies, ILogger<PositionReconciler> logger)
    {
        _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Сверяет позицию портфеля со стратегией.
    /// </summary>
    /// <returns>
    /// <see langword="true"/>, если стратегия найдена и умеет принимать
    /// фактическую позицию; иначе <see langword="false"/>.
    /// </returns>
    public bool TryReconcile(Portfolio portfolio)
    {
        ArgumentNullException.ThrowIfNull(portfolio);

        if (!_strategies.TryGet(portfolio.StrategyId, out var algo))
        {
            return false;
        }

        if (algo is not IPositionReconcilable reconcilable)
        {
            return false;
        }

        var xQtyLots = ToLots(portfolio.Get(algo.AssetX)?.Quantity ?? 0m);
        var yQtyLots = ToLots(portfolio.Get(algo.AssetY)?.Quantity ?? 0m);

        reconcilable.ReconcilePosition(xQtyLots, yQtyLots);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Position reconciled. StrategyId={StrategyId} AssetX={AssetX} XQtyLots={XQtyLots} AssetY={AssetY} YQtyLots={YQtyLots}",
                portfolio.StrategyId,
                algo.AssetX,
                xQtyLots,
                algo.AssetY,
                yQtyLots);
        }

        return true;
    }

    // Объём в портфеле накапливается из целых лотов, но хранится как decimal.
    // Округление защищает от накопленной дробной части.
    private static int ToLots(decimal quantity) => decimal.ToInt32(decimal.Round(quantity, 0, MidpointRounding.ToZero));
}
