using Market.ApiClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Market.ConsoleApp.BackgroundServices;

internal sealed class MarketInfoUpdaterService : BackgroundService
{
    private const int AssetId = 34;

    private readonly IServiceProvider _serviceProvider;
    private readonly IMarketRestApiClient _restApiClient;
    private readonly ILogger<MarketInfoUpdaterService> _logger;

    public MarketInfoUpdaterService(
        IServiceProvider serviceProvider,
        IMarketRestApiClient restApiClient,
        ILogger<MarketInfoUpdaterService> logger)
    {
        _serviceProvider = serviceProvider;
        _restApiClient = restApiClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //await using var scope = _serviceProvider.CreateAsyncScope();
        //var restApiClient = scope.ServiceProvider.GetRequiredService<IMarketRestApiClient>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var trades = await _restApiClient.GetTrades(AssetId, 5);
                _logger.LogInformation("Trades: {Trades}", string.Join(",", trades));

                var stats = await _restApiClient.GetTradesStats();
                _logger.LogInformation("Trades stats: {Stats}", stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occured: {Message}", ex.Message);
            }

            await Task.Delay(5000, stoppingToken);
        }
    }
}
