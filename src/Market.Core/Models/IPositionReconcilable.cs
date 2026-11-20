namespace Market.Core.Models;

/// <summary>
/// Алгоритм, который умеет принимать фактическую позицию из исполнения.
/// </summary>
/// <remarks>
/// Вызывается после применения каждой исполненной сделки к портфелю.
/// Объёмы передаются в лотах и являются абсолютной текущей позицией,
/// а не дельтой.
/// </remarks>
public interface IPositionReconcilable : IPairTradingAlgo
{
    /// <summary>
    /// Сверяет внутреннее состояние позиции с фактической позицией портфеля.
    /// </summary>
    /// <param name="xQtyLots">Текущая позиция по <see cref="IPairTradingAlgo.AssetX"/> в лотах.</param>
    /// <param name="yQtyLots">Текущая позиция по <see cref="IPairTradingAlgo.AssetY"/> в лотах.</param>
    void ReconcilePosition(int xQtyLots, int yQtyLots);
}
