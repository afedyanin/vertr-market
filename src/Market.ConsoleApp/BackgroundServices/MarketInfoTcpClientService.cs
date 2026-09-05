using Market.ApiClient;
using Market.ConsoleApp.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Market.ConsoleApp.BackgroundServices;

internal sealed class MarketInfoTcpClientService : BackgroundService
{
    private readonly ILogger<MarketInfoTcpClientService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly MarketApiSettings _settings;

    public MarketInfoTcpClientService(
        IServiceProvider serviceProvider,
        IOptions<MarketApiSettings> options,
        ILogger<MarketInfoTcpClientService> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var tcpConnection = scope.ServiceProvider.GetRequiredService<ITcpClientConnection>();
        var tcpClient = scope.ServiceProvider.GetRequiredService<IMarketTcpApiClient>();

        tcpConnection.OnConnected += OnClientConnected;
        tcpConnection.OnDisconnected += OnClientDisconnected;

        await tcpConnection.ConnectAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var books = await tcpClient.GetBooks(_settings.AssetId, 1) ?? [];

                if (books.Any())
                {
                    Console.Clear();
                    Console.Write(books[0].Dump());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occured: {Message}", ex.Message);
            }

            await Task.Delay(2000, stoppingToken);
        }
    }

    private void OnClientConnected(object? sender, EventArgs e)
    {
        _logger.LogInformation("TCP client connected.");
    }

    private void OnClientDisconnected(object? sender, EventArgs e)
    {
        _logger.LogInformation("TCP client disconnected.");
    }
}
