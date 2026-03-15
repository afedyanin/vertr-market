using Vertr.Common.Contracts;

namespace Vertr.Market.Application.Abstractions;

public interface IOpenInterestRepository
{
    public Task<bool> Save(DateTime timeBefore, IEnumerable<OpenInterest> openInterests);

    public IAsyncEnumerable<OpenInterest> Get(Guid instrumentId, DateTime from, DateTime to);
}
