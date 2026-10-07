using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Market.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Market.ConsoleApp.BackgroundServices;

public class DumpOrderBooksService : BackgroundService
{
    // ANSI Escape-коды для управления цветом в консоли (0 аллокаций)
    private const string ResetColor = "\x1b[0m";
    private const string RedColor = "\x1b[31m";   // Для Asks (Продавцы)
    private const string GreenColor = "\x1b[32m"; // Для Bids (Покупатели)
    private const string CyanColor = "\x1b[36m";  // Для мета-информации

    private readonly ChannelReader<OrderBookDepth> _reader;
    private readonly ILogger<DumpOrderBooksService> _logger;

    public DumpOrderBooksService(
        Channel<OrderBookDepth> channel,
        ILoggerFactory loggerFactory)
    {
        _reader = channel.Reader;
        _logger = loggerFactory.CreateLogger<DumpOrderBooksService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        _logger.LogInformation("Starting dump order books service...");

        try
        {
            await foreach (var book in _reader.ReadAllAsync(stoppingToken))
            {
                _logger.LogDebug("Order book received for AssetId={AssetId}", book.AssetId);

                if (book.AssetId == 1)
                {
                    Console.Clear();
                    Console.Write(Dump(book));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dump order books service error. Message={Message}", ex.Message);
        }

        _logger.LogInformation("Dump order books service stopped.");
    }

    public static string Dump(OrderBookDepth dto)
    {
        // Выделяем буфер с запасом, так как ANSI-коды занимают дополнительные символы
        var handler = new DefaultInterpolatedStringHandler(literalLength: 1024, formattedCount: 40);

        handler.AppendLiteral($"{CyanColor}=== Market Depth | Asset: {dto.AssetId} ==={ResetColor}\n");

        // 1. Выводим ASKS (Красный цвет)
        handler.AppendLiteral($"{RedColor}--- ASKS (Sells) ---{ResetColor}\n");
        for (var i = dto.Asks.Length - 1; i >= 0; i--)
        {
            ref readonly var level = ref dto.Asks[i];
            if (level.Size > 0 || level.Price > 0)
            {
                // Подсвечиваем только значения цен и объемов, сохраняя структуру
                handler.AppendLiteral($"  [Ask {i}] Price: {RedColor}{level.Price,-10}{ResetColor} | Volume: {RedColor}{level.Size}{ResetColor}");

                // Маркер для лучшего Ask
                if (i == 0)
                {
                    handler.AppendLiteral("  <-- Best Ask");
                }

                handler.AppendLiteral("\n");
            }
        }

        handler.AppendLiteral("--------------------\n");

        // 2. Выводим BIDS (Зеленый цвет)
        handler.AppendLiteral($"{GreenColor}--- BIDS (Buys)  ---{ResetColor}\n");
        for (var i = 0; i < dto.Bids.Length; i++)
        {
            ref readonly var level = ref dto.Bids[i];
            if (level.Size > 0 || level.Price > 0)
            {
                handler.AppendLiteral($"  [Bid {i}] Price: {GreenColor}{level.Price,-10}{ResetColor} | Volume: {GreenColor}{level.Size}{ResetColor}");

                // Маркер для лучшего Bid
                if (i == 0)
                {
                    handler.AppendLiteral("  <-- Best Bid");
                }

                handler.AppendLiteral("\n");
            }
        }

        handler.AppendLiteral($"{CyanColor}======================================================={ResetColor}");

        return handler.ToStringAndClear();
    }
}
