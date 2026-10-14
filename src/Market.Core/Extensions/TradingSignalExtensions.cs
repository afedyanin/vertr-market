using Market.Core.Models;

namespace Market.Core.Extensions;

public static class TradingSignalExtensions
{

    public static double? GetParam(this ITradingSignal signal, string key)
    {
        _ = signal.Params.TryGetValue(key, out var value);
        return value;
    }
}
