using Microsoft.Extensions.ObjectPool;

namespace Vertr.Market.Application.Quotes;

public sealed class DictionaryPoolPolicy : IPooledObjectPolicy<Dictionary<int, Quote>>
{
    private readonly int _initialCapacity;

    public DictionaryPoolPolicy(int initialCapacity = 64)
    {
        _initialCapacity = initialCapacity;
    }

    public Dictionary<int, Quote> Create()
    {
        return new Dictionary<int, Quote>(_initialCapacity);
    }

    public bool Return(Dictionary<int, Quote> obj)
    {
        obj.Clear();
        return true;
    }
}