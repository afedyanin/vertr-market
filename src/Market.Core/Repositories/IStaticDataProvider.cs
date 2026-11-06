using Market.Core.Models;

namespace Market.Core.Repositories;

public interface IStaticDataProvider
{
    public const char TickerDelimeter = ':';

    public Asset[] GetAll();
    public Asset? GetByAssetId(ushort assetId);
    public Asset? GetByExternalId(string externalId);
    public Asset? GetByTicker(string tickerWithClassCode);
    public static string ComposeTicker(string classCode, string ticker)
        => $"{classCode}{TickerDelimeter}{ticker}";

    public Task LoadFrom(string relativeDirectory, bool recursive = false, CancellationToken cancellationToken = default);
}
