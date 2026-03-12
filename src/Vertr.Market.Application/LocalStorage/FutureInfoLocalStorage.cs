using Vertr.Common.Contracts;
using Vertr.Common.Contracts.Abstractions;

namespace Vertr.Market.Application.LocalStorage;

internal sealed class FutureInfoLocalStorage : IFutureInfoRepository
{
    private readonly Dictionary<string, FutureInfo> _futuresDict = [];

    public FutureInfo[] GetAll(string? tickerWildCard = null)
        => tickerWildCard == null ? [.. _futuresDict.Values] :
            [.. _futuresDict.Values.Where(f => f.Ticker.Contains(tickerWildCard, StringComparison.OrdinalIgnoreCase))];

    public FutureInfo? Get(string ticker)
    {
        _futuresDict.TryGetValue(ticker, out var futureInfo);
        return futureInfo;
    }

    public void Load(FutureInfo[] futures)
    {
        foreach (var futureInfo in futures)
        {
            _futuresDict[futureInfo.Ticker] = futureInfo;
        }
    }
}
