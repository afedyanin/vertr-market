using Market.Core.Models;

namespace Market.Core.Repositories;

/// <summary>
/// Реестр запущенных экземпляров стратегий.
/// </summary>
/// <remarks>
/// Нужен, чтобы поток исполнения (портфель) мог найти алгоритм по
/// <c>StrategyId</c> и передать ему фактическую позицию.
/// </remarks>
public interface IStrategyRegistry
{
    /// <summary>Регистрирует экземпляр стратегии; повторная регистрация заменяет его.</summary>
    void Register(IPairTradingAlgo algo);

    /// <summary>Удаляет стратегию из реестра.</summary>
    bool Remove(Guid strategyId);

    /// <summary>Пытается получить запущенный экземпляр стратегии.</summary>
    bool TryGet(Guid strategyId, out IPairTradingAlgo algo);
}
