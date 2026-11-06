using Market.Core.Models;

namespace Market.Core.Repositories;

public interface IAlgoMetadataRepository
{
    public IEnumerable<AlgoMetadata> GetAll();

    public bool Save(AlgoMetadata algoMetadata);

    public bool Delete(Guid strategyId);

    public event Action<AlgoMetadata> OnSaved;

    public event Action<AlgoMetadata> OnDeleted;
}
