using System.Globalization;

namespace Market.BacktestEngine;

/// <summary>
/// Проверенные аргументы запуска бэктеста.
/// </summary>
public sealed record BacktestArguments(
    string DataFilePath,
    string AlgoMetadataFilePath,
    string OutputFilePath,
    DateTime? FromUtc,
    DateTime? ToUtc,
    double SlippagePercent,
    double CommissionPercent);

/// <summary>
/// Разбор аргументов командной строки бэктестера.
/// </summary>
public static class BacktestArgumentsParser
{
    public const string DefaultOutputFilePath = "portfolio.json";

    public const string Usage = """
        Market.BacktestEngine — офлайн-бэктест торговой стратегии.

        Обязательные параметры:
          --data <path>               Файл исторических стаканов (books_*.bin.gz).
          --metadata <path>           JSON-файл с AlgoMetadata стратегии.

        Необязательные параметры:
          --output <path>             Куда сохранить Portfolio в JSON (по умолчанию portfolio.json).
          --from <date-time>          Начало интервала (включительно), ISO 8601; UTC, если зона не указана.
          --to <date-time>            Конец интервала (включительно), ISO 8601; UTC, если зона не указана.
          --slippage-percent <value>  Проскальзывание в процентах, по умолчанию 0.
          --commission-percent <value> Комиссия в процентах от объёма, по умолчанию 0.
          -h, --help                  Показать эту справку.

        Пример:
          dotnet run --project src/Market.BacktestEngine -- \
            --data tests/Market.Core.Tests/Data/books_2026-10-05.bin.gz \
            --metadata metadata.json \
            --from 2026-10-05T10:00:00Z \
            --to 2026-10-05T18:00:00Z \
            --output portfolio.json
        """;

    public static bool IsHelpRequested(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        foreach (var arg in args)
        {
            if (arg is "--help" or "-h")
            {
                return true;
            }
        }

        return false;
    }

    public static BacktestArguments Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? dataFilePath = null;
        string? metadataFilePath = null;
        string? outputFilePath = null;
        DateTime? fromUtc = null;
        DateTime? toUtc = null;
        var slippagePercent = default(double?);
        var commissionPercent = default(double?);

        for (var i = 0; i < args.Count; i++)
        {
            var option = args[i];

            switch (option)
            {
                case "--data":
                    EnsureNotSet(dataFilePath, option);
                    dataFilePath = ReadValue(args, ref i);
                    break;

                case "--metadata":
                    EnsureNotSet(metadataFilePath, option);
                    metadataFilePath = ReadValue(args, ref i);
                    break;

                case "--output":
                    EnsureNotSet(outputFilePath, option);
                    outputFilePath = ReadValue(args, ref i);
                    break;

                case "--from":
                    EnsureNotSet(fromUtc, option);
                    fromUtc = ParseUtcDate(ReadValue(args, ref i), option);
                    break;

                case "--to":
                    EnsureNotSet(toUtc, option);
                    toUtc = ParseUtcDate(ReadValue(args, ref i), option);
                    break;

                case "--slippage-percent":
                    EnsureNotSet(slippagePercent, option);
                    slippagePercent = ParsePercent(ReadValue(args, ref i), option);
                    break;

                case "--commission-percent":
                    EnsureNotSet(commissionPercent, option);
                    commissionPercent = ParsePercent(ReadValue(args, ref i), option);
                    break;

                default:
                    throw new BacktestConfigurationException(
                        $"Неизвестный аргумент '{option}'. Используйте --help для списка параметров.");
            }
        }

        if (dataFilePath is null)
        {
            throw new BacktestConfigurationException("Не задан обязательный параметр '--data'.");
        }

        if (metadataFilePath is null)
        {
            throw new BacktestConfigurationException("Не задан обязательный параметр '--metadata'.");
        }

        if (fromUtc is not null && toUtc is not null && fromUtc > toUtc)
        {
            throw new BacktestConfigurationException(
                $"'--from' ({fromUtc:O}) не может быть позже '--to' ({toUtc:O}).");
        }

        return new BacktestArguments(
            dataFilePath,
            metadataFilePath,
            outputFilePath ?? DefaultOutputFilePath,
            fromUtc,
            toUtc,
            slippagePercent ?? 0.0,
            commissionPercent ?? 0.0);
    }

    private static string ReadValue(IReadOnlyList<string> args, ref int index)
    {
        var option = args[index];

        if (index + 1 >= args.Count)
        {
            throw new BacktestConfigurationException($"Не задано значение для '{option}'.");
        }

        var value = args[++index];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BacktestConfigurationException($"Пустое значение для '{option}'.");
        }

        return value;
    }

    private static DateTime ParseUtcDate(string value, string option)
    {
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            throw new BacktestConfigurationException(
                $"Некорректная дата для '{option}': '{value}'. Используйте ISO 8601, например 2026-10-05T10:00:00Z.");
        }

        return parsed.UtcDateTime;
    }

    private static double ParsePercent(string value, string option)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed)
            || parsed < 0.0
            || parsed >= 100.0)
        {
            throw new BacktestConfigurationException(
                $"Некорректное значение для '{option}': '{value}'. Ожидается число в интервале [0, 100).");
        }

        return parsed;
    }

    private static void EnsureNotSet(object? value, string option)
    {
        if (value is not null)
        {
            throw new BacktestConfigurationException($"Параметр '{option}' указан несколько раз.");
        }
    }
}
