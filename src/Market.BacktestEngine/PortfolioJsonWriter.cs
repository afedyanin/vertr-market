using System.Text.Json;
using System.Text.Json.Serialization;
using Market.Core.Models;

namespace Market.BacktestEngine;

/// <summary>
/// Сохранение результата бэктеста в JSON.
/// </summary>
public static class PortfolioJsonWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Записывает портфель в файл, создавая недостающие каталоги.
    /// </summary>
    public static async Task WriteAsync(
        string filePath,
        Portfolio portfolio,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(portfolio);

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await JsonSerializer
            .SerializeAsync(stream, portfolio, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }
}
