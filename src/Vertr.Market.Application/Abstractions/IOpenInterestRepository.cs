using Vertr.Common.Contracts;

namespace Vertr.Market.Application.Abstractions;

public interface IOpenInterestRepository
{
    public Task<bool> Save(DateTime timeBefore, IEnumerable<OpenInterest> openInterests);
}
