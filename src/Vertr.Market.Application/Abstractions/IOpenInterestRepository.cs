using Vertr.Common.Contracts;

namespace Vertr.Market.Application.Abstractions;

public interface IOpenInterestRepository
{
    public Task<int?> Save(DateTime timeBefore, OpenInterest[] openInterests);

    public IAsyncEnumerable<OpenInterest> Get(Guid instrumentId, DateTime from, DateTime to);
}
