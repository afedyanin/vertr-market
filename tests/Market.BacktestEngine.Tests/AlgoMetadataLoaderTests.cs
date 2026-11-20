using Market.Core.Algos.PairTrading;
using Market.Core.Algos.PairTradingV2;

namespace Market.BacktestEngine.Tests;

public class AlgoMetadataLoaderTests
{
    private const string KalmanBasicJson = """
        {
          "strategyId": "3fa85f64-5717-4562-b3fc-2c963f66afa8",
          "assetX": 1,
          "assetY": 5,
          "algoType": "KalmanFilterBasicStrategy",
          "algoParams": {
            "EntryThreshold": 2.0,
            "ExitThreshold": 0.5,
            "MaxLots": 10,
            "WarmupTicks": 50
          }
        }
        """;

    private const string KalmanMidPriceJson = """
        {
          "strategyId": "3fa85f64-5717-4562-b3fc-2c963f66afa8",
          "assetX": 1,
          "assetY": 5,
          "algoType": "KalmanMidPriceAlgo",
          "algoParams": {
            "LotSizeX": 1,
            "LotSizeY": 100,
            "InitialBeta": 1.0,
            "InitialErrorCovariance": 1.0
          }
        }
        """;

    [Test]
    public void ParseJson_ValidMetadata_ReturnsFields()
    {
        var metadata = AlgoMetadataLoader.ParseJson(KalmanBasicJson);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.StrategyId, Is.EqualTo(Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa8")));
            Assert.That(metadata.AssetX, Is.EqualTo(1));
            Assert.That(metadata.AssetY, Is.EqualTo(5));
            Assert.That(metadata.AlgoType, Is.EqualTo("KalmanFilterBasicStrategy"));
            Assert.That(metadata.AlgoParams["EntryThreshold"], Is.EqualTo(2.0));
            Assert.That(metadata.AlgoParams["MaxLots"], Is.EqualTo(10.0));
        });
    }

    [Test]
    public void ParseJson_AlgoParamsOmitted_ReturnsEmptyDictionary()
    {
        const string json = """
            {
              "strategyId": "3fa85f64-5717-4562-b3fc-2c963f66afa8",
              "assetX": 1,
              "assetY": 5,
              "algoType": "KalmanFilterBasicStrategy"
            }
            """;

        var metadata = AlgoMetadataLoader.ParseJson(json);

        Assert.That(metadata.AlgoParams, Is.Empty);
    }

    [Test]
    public void ParseJson_PropertyNamesAreCaseInsensitive()
    {
        const string json = """
            {
              "STRATEGYID": "3fa85f64-5717-4562-b3fc-2c963f66afa8",
              "AssetX": 1,
              "ASSETY": 5,
              "AlgoType": "KalmanFilterBasicStrategy",
              "AlgoParams": { "EntryThreshold": 2.0, "ExitThreshold": 0.5 }
            }
            """;

        var metadata = AlgoMetadataLoader.ParseJson(json);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.AssetX, Is.EqualTo(1));
            Assert.That(metadata.AssetY, Is.EqualTo(5));
            Assert.That(metadata.AlgoType, Is.EqualTo("KalmanFilterBasicStrategy"));
        });
    }

    [Test]
    public void ParseJson_InvalidJson_Throws()
    {
        Assert.Throws<BacktestConfigurationException>(() =>
            _ = AlgoMetadataLoader.ParseJson("{ not json"));
    }

    [Test]
    public void ParseJson_MissingAlgoType_Throws()
    {
        const string json = """
            {
              "strategyId": "3fa85f64-5717-4562-b3fc-2c963f66afa8",
              "assetX": 1,
              "assetY": 5
            }
            """;

        Assert.Throws<BacktestConfigurationException>(() =>
            _ = AlgoMetadataLoader.ParseJson(json));
    }

    [Test]
    public void ParseJson_EmptyStrategyId_Throws()
    {
        const string json = """
            {
              "assetX": 1,
              "assetY": 5,
              "algoType": "KalmanFilterBasicStrategy"
            }
            """;

        Assert.Throws<BacktestConfigurationException>(() =>
            _ = AlgoMetadataLoader.ParseJson(json));
    }

    [Test]
    public void ParseJson_SameAssets_Throws()
    {
        const string json = """
            {
              "strategyId": "3fa85f64-5717-4562-b3fc-2c963f66afa8",
              "assetX": 5,
              "assetY": 5,
              "algoType": "KalmanFilterBasicStrategy"
            }
            """;

        Assert.Throws<BacktestConfigurationException>(() =>
            _ = AlgoMetadataLoader.ParseJson(json));
    }

    [Test]
    public void CreateStrategy_KalmanBasic_ReturnsExpectedType()
    {
        var metadata = AlgoMetadataLoader.ParseJson(KalmanBasicJson);

        var strategy = AlgoMetadataLoader.CreateStrategy(metadata);

        Assert.That(strategy, Is.InstanceOf<KalmanFilterBasicStrategy>());
    }

    [Test]
    public void CreateStrategy_KalmanMidPrice_ReturnsExpectedType()
    {
        var metadata = AlgoMetadataLoader.ParseJson(KalmanMidPriceJson);

        var strategy = AlgoMetadataLoader.CreateStrategy(metadata);

        Assert.That(strategy, Is.InstanceOf<KalmanMidPriceAlgo>());
    }

    [Test]
    public void CreateStrategy_UnknownAlgoType_ThrowsConfigurationError()
    {
        const string json = """
            {
              "strategyId": "3fa85f64-5717-4562-b3fc-2c963f66afa8",
              "assetX": 1,
              "assetY": 5,
              "algoType": "UnknownAlgo"
            }
            """;

        var metadata = AlgoMetadataLoader.ParseJson(json);

        var exception = Assert.Throws<BacktestConfigurationException>(
            () => AlgoMetadataLoader.CreateStrategy(metadata));

        Assert.That(exception!.Message, Does.Contain("UnknownAlgo"));
    }

    [TestCase("EntryThreshold", "0")]
    [TestCase("ExitThreshold", "2.0")]
    [TestCase("LotSizeX", "0")]
    [TestCase("LotSizeY", "0")]
    [TestCase("MaxLots", "0")]
    [TestCase("WarmupTicks", "-1")]
    [TestCase("WarmupTicks", "1.5")]
    [TestCase("Delta", "0")]
    [TestCase("Delta", "1")]
    [TestCase("ObsNoise", "0")]
    [TestCase("PriceRatio", "0")]
    [TestCase("FeeRateX", "-0.1")]
    public void CreateStrategy_InvalidConstructorParameters_ThrowsConfigurationError(string key, string value)
    {
        var json = CreateMetadataWithParam(key, value);
        var metadata = AlgoMetadataLoader.ParseJson(json);

        Assert.Throws<BacktestConfigurationException>(
            () => AlgoMetadataLoader.CreateStrategy(metadata));
    }

    [Test]
    public void Load_MissingFile_Throws()
    {
        Assert.Throws<BacktestConfigurationException>(() =>
            _ = AlgoMetadataLoader.Load(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json")));
    }

    [Test]
    public void Load_ExistingFile_ReturnsMetadata()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, KalmanBasicJson);

            var metadata = AlgoMetadataLoader.Load(path);

            Assert.Multiple(() =>
            {
                Assert.That(metadata.AlgoType, Is.EqualTo("KalmanFilterBasicStrategy"));
                Assert.That(metadata.AssetX, Is.EqualTo(1));
                Assert.That(metadata.AssetY, Is.EqualTo(5));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateMetadataWithParam(string key, string value) => $$"""
        {
          "strategyId": "3fa85f64-5717-4562-b3fc-2c963f66afa8",
          "assetX": 1,
          "assetY": 5,
          "algoType": "KalmanFilterBasicStrategy",
          "algoParams": {
            "{{key}}": {{value}}
          }
        }
        """;
}
