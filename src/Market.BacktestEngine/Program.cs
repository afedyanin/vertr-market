using Market.Core.FileStore;
using Market.Infrastructure;

namespace Market.BacktestEngine;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (BacktestArgumentsParser.IsHelpRequested(args))
        {
            await Console.Out.WriteLineAsync(BacktestArgumentsParser.Usage).ConfigureAwait(false);
            return 0;
        }

        using var cancellation = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            var arguments = BacktestArgumentsParser.Parse(args);

            if (!File.Exists(arguments.DataFilePath))
            {
                throw new BacktestConfigurationException(
                    $"Файл исторических данных не найден: {arguments.DataFilePath}");
            }

            var metadata = AlgoMetadataLoader.Load(arguments.AlgoMetadataFilePath);
            var algo = AlgoMetadataLoader.CreateStrategy(metadata);

            var executionOptions = new BacktestExecutionOptions
            {
                SlippagePercent = arguments.SlippagePercent,
                CommissionPercent = arguments.CommissionPercent,
            };

            await Console.Out.WriteLineAsync(
                $"Бэктест: algo={metadata.AlgoType}, assetX={metadata.AssetX}, assetY={metadata.AssetY}, " +
                $"data={arguments.DataFilePath}").ConfigureAwait(false);

            var result = await BacktestRunner.RunAsync(
                new BacktestRequest(
                    algo,
                    OrderBookFileReader.ReadAsync(arguments.DataFilePath, cancellation.Token),
                    executionOptions,
                    arguments.FromUtc,
                    arguments.ToUtc),
                cancellation.Token).ConfigureAwait(false);

            await PortfolioJsonWriter.WriteAsync(
                arguments.OutputFilePath,
                result.Portfolio,
                cancellation.Token).ConfigureAwait(false);

            var cash = result.Portfolio.Positions.Sum(position => position.Cash);
            var outputPath = Path.GetFullPath(arguments.OutputFilePath);

            await Console.Out.WriteLineAsync(
                $"Готово: books={result.ProcessedBooks}, signals={result.SignalCount}, " +
                $"trades={result.TradeCount}, liquidationTrades={result.LiquidationTradeCount}, " +
                $"cash={cash:0.####}, commission={result.Portfolio.Commission:0.####}").ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Портфель сохранён: {outputPath}").ConfigureAwait(false);

            return 0;
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("Бэктест отменён.").ConfigureAwait(false);
            return 130;
        }
        catch (BacktestConfigurationException ex)
        {
            await Console.Error.WriteLineAsync($"Ошибка конфигурации: {ex.Message}").ConfigureAwait(false);
            await Console.Error.WriteLineAsync().ConfigureAwait(false);
            await Console.Error.WriteLineAsync(BacktestArgumentsParser.Usage).ConfigureAwait(false);
            return 2;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Бэктест завершился ошибкой: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }
}
