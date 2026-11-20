using System.Text.Json;
using Market.Core.Algos;
using Market.Core.Models;

namespace Market.BacktestEngine;

/// <summary>
/// Загрузка и проверка метаданных стратегии для бэктеста.
/// </summary>
public static class AlgoMetadataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Читает <see cref="AlgoMetadata"/> из JSON-файла.
    /// </summary>
    public static AlgoMetadata Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new BacktestConfigurationException($"Файл метаданных не найден: {filePath}");
        }

        return ParseJson(File.ReadAllText(filePath));
    }

    /// <summary>
    /// Разбирает и проверяет JSON с метаданными стратегии.
    /// </summary>
    public static AlgoMetadata ParseJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        AlgoMetadata? metadata;

        try
        {
            metadata = JsonSerializer.Deserialize<AlgoMetadata>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new BacktestConfigurationException($"Некорректный JSON метаданных стратегии: {ex.Message}", ex);
        }

        if (metadata is null)
        {
            throw new BacktestConfigurationException("JSON метаданных стратегии пуст.");
        }

        Validate(metadata);
        return metadata;
    }

    /// <summary>
    /// Создаёт стратегию через <see cref="AlgoFactory"/> и проверяет её параметры.
    /// </summary>
    public static IPairTradingAlgo CreateStrategy(AlgoMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Validate(metadata);

        try
        {
            return AlgoFactory.Create(metadata);
        }
        catch (Exception ex) when (ex is ArgumentException or NotImplementedException)
        {
            throw new BacktestConfigurationException(
                $"Не удалось создать стратегию '{metadata.AlgoType}': {ex.Message}",
                ex);
        }
    }

    private static void Validate(AlgoMetadata metadata)
    {
        if (metadata.StrategyId == Guid.Empty)
        {
            throw new BacktestConfigurationException("В метаданных не задан 'strategyId'.");
        }

        if (string.IsNullOrWhiteSpace(metadata.AlgoType))
        {
            throw new BacktestConfigurationException("В метаданных не задан 'algoType'.");
        }

        if (metadata.AssetX == metadata.AssetY)
        {
            throw new BacktestConfigurationException(
                $"'assetX' и 'assetY' должны различаться, получено {metadata.AssetX}.");
        }
    }
}
