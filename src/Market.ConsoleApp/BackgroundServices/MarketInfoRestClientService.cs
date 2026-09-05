using Market.ApiClient;
using Market.ConsoleApp.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Market.ConsoleApp.BackgroundServices;

internal sealed class MarketInfoRestClientService : BackgroundService
{
    private readonly IMarketRestApiClient _restApiClient;
    private readonly ILogger<MarketInfoRestClientService> _logger;

    private readonly MarketApiSettings _settings;

    public MarketInfoRestClientService(
        IMarketRestApiClient restApiClient,
        IOptions<MarketApiSettings> options,
        ILogger<MarketInfoRestClientService> logger)
    {
        _restApiClient = restApiClient;
        _settings = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var book = await _restApiClient.GetBooks(_settings.AssetId, 1) ?? [];

                if (book.Any())
                {
                    Console.Clear();
                    Console.Write(book[0].Dump());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occured: {Message}", ex.Message);
            }

            await Task.Delay(2000, stoppingToken);
        }
    }
}
