using System.Text.Json;
using Market.Core.Models;

namespace Market.BacktestEngine.Tests;

public class PortfolioJsonWriterTests
{
    [Test]
    public async Task WriteAsync_CreatesDirectoryAndWritesPortfolio()
    {
        // Arrange
        var strategyId = Guid.NewGuid();
        var portfolio = new Portfolio { StrategyId = strategyId };
        portfolio.Update(new MyTrade
        {
            StrategyId = strategyId,
            AssetId = 1,
            Qty = 2m,
            Price = 100m,
            Commission = 0.1m,
            Timestamp = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc),
        });

        var directory = Path.Combine(Path.GetTempPath(), $"backtest-{Guid.NewGuid():N}", "nested");
        var path = Path.Combine(directory, "portfolio.json");

        try
        {
            // Act
            await PortfolioJsonWriter.WriteAsync(path, portfolio);

            // Assert
            Assert.That(File.Exists(path), Is.True);

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var root = document.RootElement;

            Assert.Multiple(() =>
            {
                Assert.That(
                    root.GetProperty("strategyId").GetGuid(),
                    Is.EqualTo(strategyId));

                var position = root.GetProperty("positions")[0];
                Assert.That(position.GetProperty("assetId").GetUInt16(), Is.EqualTo(1));
                Assert.That(position.GetProperty("quantity").GetDecimal(), Is.EqualTo(2m));
                Assert.That(position.GetProperty("commission").GetDecimal(), Is.EqualTo(0.1m));

                var trade = root.GetProperty("trades")[0];
                Assert.That(trade.GetProperty("price").GetDecimal(), Is.EqualTo(100m));
                Assert.That(trade.GetProperty("commission").GetDecimal(), Is.EqualTo(0.1m));

                Assert.That(root.GetProperty("commission").GetDecimal(), Is.EqualTo(0.1m));
            });
        }
        finally
        {
            var rootDirectory = Path.GetDirectoryName(Path.GetDirectoryName(path));
            if (rootDirectory is not null && Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Test]
    public async Task WriteAsync_OverwritesExistingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"portfolio-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(path, "old content");

            await PortfolioJsonWriter.WriteAsync(
                path,
                new Portfolio { StrategyId = Guid.NewGuid() });

            var content = await File.ReadAllTextAsync(path);

            Assert.That(content, Does.Contain("\"strategyId\""));
            Assert.That(content, Does.Not.Contain("old content"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
