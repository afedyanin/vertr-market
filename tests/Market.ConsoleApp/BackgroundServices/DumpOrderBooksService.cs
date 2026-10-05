using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Market.Core.Models;
using Market.Core.Extensions;
using System.Threading.Channels;

namespace Market.ConsoleApp.BackgroundServices;

public class DumpOrderBooksService : BackgroundService
{
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
                    Console.Write(book.Dump());
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
}
