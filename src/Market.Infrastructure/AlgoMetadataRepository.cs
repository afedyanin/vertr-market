using System.Collections.Concurrent;
using Market.Core.Models;

namespace Market.Infrastructure;

internal sealed class AlgoMetadataRepository : IAlgoMetadataRepository
{
    private readonly ConcurrentDictionary<Guid, AlgoMetadata> _algos = [];

    public event Action<AlgoMetadata>? OnSaved;
    public event Action<AlgoMetadata>? OnDeleted;

    public IEnumerable<AlgoMetadata> GetAll()
        => _algos.Values;

    public bool Save(AlgoMetadata algoMetadata)
    {
        var saved = _algos.TryAdd(algoMetadata.StrategyId, algoMetadata);

        if (saved)
        {
            OnSaved?.Invoke(algoMetadata);
        }

        return saved;
    }

    public bool Delete(Guid strategyId)
    {
        var deleted = _algos.TryRemove(strategyId, out var meta);

        if (deleted && meta is not null)
        {
            OnDeleted?.Invoke(meta);
        }

        return deleted;
    }
}
