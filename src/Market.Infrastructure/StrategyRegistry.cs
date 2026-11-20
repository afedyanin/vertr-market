using System.Collections.Concurrent;
using Market.Core.Models;
using Market.Core.Repositories;

namespace Market.Infrastructure;

/// <summary>
/// Потокобезопасный реестр запущенных стратегий.
/// </summary>
public sealed class StrategyRegistry : IStrategyRegistry
{
    private readonly ConcurrentDictionary<Guid, IPairTradingAlgo> _algorithms = [];

    public void Register(IPairTradingAlgo algo)
    {
        ArgumentNullException.ThrowIfNull(algo);
        _algorithms[algo.StrategyId] = algo;
    }

    public bool Remove(Guid strategyId) => _algorithms.TryRemove(strategyId, out _);

    public bool TryGet(Guid strategyId, out IPairTradingAlgo algo)
    {
        var found = _algorithms.TryGetValue(strategyId, out var value);
        algo = value!;
        return found;
    }
}
