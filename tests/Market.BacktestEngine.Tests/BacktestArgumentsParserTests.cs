namespace Market.BacktestEngine.Tests;

public class BacktestArgumentsParserTests
{
    [Test]
    public void Parse_FullArguments_ReturnsAllValues()
    {
        // Arrange
        var args = new[]
        {
            "--data", "Data\\books.bin.gz",
            "--metadata", "algo.json",
            "--output", "results\\portfolio.json",
            "--from", "2026-10-05T10:00:00+03:00",
            "--to", "2026-10-05T18:00:00Z",
            "--slippage-percent", "0.1",
            "--commission-percent", "0.05",
        };

        // Act
        var result = BacktestArgumentsParser.Parse(args);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.DataFilePath, Is.EqualTo("Data\\books.bin.gz"));
            Assert.That(result.AlgoMetadataFilePath, Is.EqualTo("algo.json"));
            Assert.That(result.OutputFilePath, Is.EqualTo("results\\portfolio.json"));
            Assert.That(result.FromUtc, Is.EqualTo(new DateTime(2026, 10, 5, 7, 0, 0, DateTimeKind.Utc)));
            Assert.That(result.ToUtc, Is.EqualTo(new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc)));
            Assert.That(result.SlippagePercent, Is.EqualTo(0.1));
            Assert.That(result.CommissionPercent, Is.EqualTo(0.05));
        });
    }

    [Test]
    public void Parse_MinimalArguments_UsesDefaults()
    {
        var result = BacktestArgumentsParser.Parse(
        [
            "--data", "books.bin.gz",
            "--metadata", "algo.json",
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.OutputFilePath, Is.EqualTo(BacktestArgumentsParser.DefaultOutputFilePath));
            Assert.That(result.FromUtc, Is.Null);
            Assert.That(result.ToUtc, Is.Null);
            Assert.That(result.SlippagePercent, Is.Zero);
            Assert.That(result.CommissionPercent, Is.Zero);
        });
    }

    [Test]
    public void Parse_DateWithoutTimeZone_IsTreatedAsUtc()
    {
        var result = BacktestArgumentsParser.Parse(
        [
            "--data", "books.bin.gz",
            "--metadata", "algo.json",
            "--from", "2026-10-05T10:00:00",
        ]);

        Assert.That(result.FromUtc, Is.EqualTo(new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public void Parse_DateOnly_IsMidnightUtc()
    {
        var result = BacktestArgumentsParser.Parse(
        [
            "--data", "books.bin.gz",
            "--metadata", "algo.json",
            "--to", "2026-10-06",
        ]);

        Assert.That(result.ToUtc, Is.EqualTo(new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public void Parse_FromEqualsTo_IsAllowed()
    {
        var result = BacktestArgumentsParser.Parse(
        [
            "--data", "books.bin.gz",
            "--metadata", "algo.json",
            "--from", "2026-10-05T10:00:00Z",
            "--to", "2026-10-05T10:00:00Z",
        ]);

        Assert.That(result.FromUtc, Is.EqualTo(result.ToUtc));
    }

    [Test]
    public void Parse_MissingData_Throws()
    {
        Assert.Throws<BacktestConfigurationException>(() =>
            _ = BacktestArgumentsParser.Parse(["--metadata", "algo.json"]));
    }

    [Test]
    public void Parse_MissingMetadata_Throws()
    {
        Assert.Throws<BacktestConfigurationException>(() =>
            _ = BacktestArgumentsParser.Parse(["--data", "books.bin.gz"]));
    }

    [Test]
    public void Parse_MissingValue_Throws()
    {
        Assert.Throws<BacktestConfigurationException>(() =>
            _ = BacktestArgumentsParser.Parse(["--data"]));
    }

    [Test]
    public void Parse_EmptyValue_Throws()
    {
        Assert.Throws<BacktestConfigurationException>(() =>
            _ = BacktestArgumentsParser.Parse(["--data", "  ", "--metadata", "algo.json"]));
    }

    [Test]
    public void Parse_UnknownArgument_Throws()
    {
        Assert.Throws<BacktestConfigurationException>(() =>
            _ = BacktestArgumentsParser.Parse(["--data", "books.bin.gz", "--metadata", "algo.json", "--oops"]));
    }

    [Test]
    public void Parse_DuplicateArgument_Throws()
    {
        Assert.Throws<BacktestConfigurationException>(() =>
            _ = BacktestArgumentsParser.Parse(
            [
                "--data", "books.bin.gz",
                "--data", "other.bin.gz",
                "--metadata", "algo.json",
            ]));
    }

    [Test]
    public void Parse_InvalidDate_Throws()
    {
        Assert.Throws<BacktestConfigurationException>(() =>
            _ = BacktestArgumentsParser.Parse(
            [
                "--data", "books.bin.gz",
                "--metadata", "algo.json",
                "--from", "not-a-date",
            ]));
    }

    [Test]
    public void Parse_FromAfterTo_Throws()
    {
        Assert.Throws<BacktestConfigurationException>(() =>
            _ = BacktestArgumentsParser.Parse(
            [
                "--data", "books.bin.gz",
                "--metadata", "algo.json",
                "--from", "2026-10-06T00:00:00Z",
                "--to", "2026-10-05T00:00:00Z",
            ]));
    }

    [TestCase("-0.1")]
    [TestCase("100")]
    [TestCase("123.45")]
    [TestCase("abc")]
    [TestCase("NaN")]
    [TestCase("Infinity")]
    public void Parse_InvalidSlippage_Throws(string value)
    {
        Assert.Throws<BacktestConfigurationException>(() =>
            _ = BacktestArgumentsParser.Parse(
            [
                "--data", "books.bin.gz",
                "--metadata", "algo.json",
                "--slippage-percent", value,
            ]));
    }

    [TestCase("-0.1")]
    [TestCase("100")]
    [TestCase("abc")]
    public void Parse_InvalidCommission_Throws(string value)
    {
        Assert.Throws<BacktestConfigurationException>(() =>
            _ = BacktestArgumentsParser.Parse(
            [
                "--data", "books.bin.gz",
                "--metadata", "algo.json",
                "--commission-percent", value,
            ]));
    }

    [TestCase("--help")]
    [TestCase("-h")]
    public void IsHelpRequested_ReturnsTrue(string value)
    {
        Assert.That(BacktestArgumentsParser.IsHelpRequested([value]), Is.True);
    }

    [Test]
    public void IsHelpRequested_NoFlag_ReturnsFalse()
    {
        Assert.That(
            BacktestArgumentsParser.IsHelpRequested(["--data", "books.bin.gz"]),
            Is.False);
    }
}
