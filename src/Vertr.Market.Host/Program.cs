using System.Diagnostics;
using Serilog;
using StackExchange.Redis;
using Vertr.Common.Clients.Tinvest;
using Vertr.Common.Contracts.Abstractions;
using Vertr.Market.Application;
using Vertr.Market.Host.BackgroundServices;

namespace Vertr.Market.Host;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var configuration = builder.Configuration;

        // add redis
        var redisConnectionString = configuration.GetConnectionString("RedisConnection");
        Debug.Assert(!string.IsNullOrEmpty(redisConnectionString));
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        // add Tinvest
        var tinvestGatewayUrl = configuration.GetValue<string>("TinvestGateway:BaseAddress");
        Debug.Assert(!string.IsNullOrEmpty(tinvestGatewayUrl));
        builder.Services.AddTinvestGateway(tinvestGatewayUrl);

        // add Logging
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .CreateBootstrapLogger();

        builder.Host.UseSerilog((context, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration) // Read from appsettings.json
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
            .Enrich.WithThreadId());

        // add Background service
        builder.Services.AddHostedService<OrderBookSubscriber>();
        builder.Services.AddHostedService<MarketTradeSubscriber>();

        builder.Services.AddApplication();

        var app = builder.Build();

        await LoadInstruments(app.Services);

        await app.RunAsync();
    }

    private static async Task LoadInstruments(IServiceProvider serviceProvider)
    {
        var tinvestGateway = serviceProvider.GetRequiredService<ITradingGateway>();
        var instrumentsLocalStorage = serviceProvider.GetRequiredService<IInstrumentsLocalStorage>();

        var instruments = await tinvestGateway.GetAllInstruments();
        instrumentsLocalStorage.Load(instruments);
    }
}
