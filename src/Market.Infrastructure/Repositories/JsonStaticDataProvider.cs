using System.Text.Json;
using Market.Core.Models;
using Market.Core.Repositories;

namespace Market.Infrastructure.Repositories;

internal sealed class JsonStaticDataProvider : IStaticDataProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<ushort, Asset> _assetsById = [];
    private readonly Dictionary<string, Asset> _assetsByExternalId = [];
    private readonly Dictionary<string, Asset> _assetsByTicker = [];

    public async Task LoadFrom(string relativeDirectory, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var baseDir = Directory.GetCurrentDirectory();
        var fullPath = Path.GetFullPath(Path.Combine(baseDir, relativeDirectory));

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {fullPath}");
        }

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(fullPath, "*.json", option);

        var assets = new List<Asset>();
        foreach (var file in files)
        {
            var items = await ReadArrayAsync(file, cancellationToken);
            assets.AddRange(items);
        }

        foreach (var asset in assets)
        {
            _assetsById[asset.Id] = asset;

            if (!string.IsNullOrEmpty(asset.ExternalId))
            {
                _assetsByExternalId[asset.ExternalId] = asset;
            }

            var tickerKey = IStaticDataProvider.ComposeTicker(asset.ClassCode, asset.Ticker);
            _assetsByTicker[tickerKey] = asset;
        }
    }
    public Asset[] GetAll()
        => [.. _assetsById.Values];

    public Asset? GetByAssetId(ushort assetId)
    {
        _assetsById.TryGetValue(assetId, out var asset);
        return asset;
    }

    public Asset? GetByExternalId(string externalId)
    {
        _assetsByExternalId.TryGetValue(externalId, out var asset);
        return asset;
    }

    public Asset? GetByTicker(string tickerWithClassCode)
    {
        _assetsByTicker.TryGetValue(tickerWithClassCode, out var asset);
        return asset;
    }

    private static async Task<IEnumerable<Asset>> ReadArrayAsync(string path, CancellationToken ct = default)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        return await JsonSerializer.DeserializeAsync<Asset[]>(stream, SerializerOptions, ct) ?? [];
    }
}
