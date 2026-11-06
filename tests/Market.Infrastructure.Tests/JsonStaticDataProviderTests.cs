using Market.Core.Repositories;
using Market.Infrastructure.Repositories;

namespace Market.Infrastructure.Tests;

public class JsonStaticDataProviderTests
{
    private const string StaticDataDirectory = "JsonStaticData";

    private IStaticDataProvider _staticDataProvider;

    [SetUp]
    public async Task SetUp()
    {
        var provider = new JsonStaticDataProvider();
        await provider.LoadFrom(StaticDataDirectory);
        _staticDataProvider = provider;
    }

    [Test]
    public void CanGetAllAssets()
    {
        var assets = _staticDataProvider.GetAll();

        Assert.That(assets, Has.Length.GreaterThan(1));

        foreach (var asset in assets)
        {
            Console.WriteLine(asset);
        }
    }

    [TestCase(12, "e6123145-9665-43e0-8413-cd61b8aa9b13")]
    public void CanGetAssetById(int assetId, string expectedExternalId)
    {
        var asset = _staticDataProvider.GetByAssetId((ushort)assetId);

        Assert.That(asset, Is.Not.Null);
        Assert.That(asset.ExternalId, Is.EqualTo(expectedExternalId));

        Console.WriteLine(asset);
    }

    [TestCase("e6123145-9665-43e0-8413-cd61b8aa9b13", 12)]
    public void CanGetAssetByExternalId(string externalId, int expectedAssetId)
    {
        var asset = _staticDataProvider.GetByExternalId(externalId);

        Assert.That(asset, Is.Not.Null);
        Assert.That(asset.Id, Is.EqualTo((ushort)expectedAssetId));

        Console.WriteLine(asset);
    }

    [TestCase("TQBR", "SBER", 12)]
    public void CanGetAssetByTicker(string classCode, string ticker, int expectedAssetId)
    {
        var composedTicker = IStaticDataProvider.ComposeTicker(classCode, ticker);
        var asset = _staticDataProvider.GetByTicker(composedTicker);

        Assert.That(asset, Is.Not.Null);
        Assert.That(asset.Id, Is.EqualTo((ushort)expectedAssetId));

        Console.WriteLine(asset);
    }
}
