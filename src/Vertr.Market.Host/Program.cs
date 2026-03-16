using System.Diagnostics;
using Serilog;
using StackExchange.Redis;
using Vertr.Common.Clients.Moex;
using Vertr.Common.Clients.Tinvest;
using Vertr.Market.Host.BackgroundServices;
using Vertr.Market.Application;
using Vertr.Market.DataAccess;

namespace Vertr.Market.Host;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var configuration = builder.Configuration;

        var redisConnectionString = configuration.GetConnectionString("RedisConnection");
        Debug.Assert(!string.IsNullOrEmpty(redisConnectionString));
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        var pgSqlConnectionString = configuration.GetConnectionString("MarketDataDbConnection");
        builder.Services.AddMarketDataAccess(pgSqlConnectionString!);

        var tinvestGatewayUrl = configuration.GetValue<string>("TinvestGateway:BaseAddress");
        Debug.Assert(!string.IsNullOrEmpty(tinvestGatewayUrl));
        builder.Services.AddTinvestGateway(tinvestGatewayUrl);

        builder.Services.AddMoexApiClient();
        builder.Services.AddApplication();

        builder.Services.AddHostedService<MarketOrderBookSubscriber>();
        builder.Services.AddHostedService<MarketOrderBookPersistenceService>();

        builder.Services.AddHostedService<MarketTradeSubscriber>();
        builder.Services.AddHostedService<MarketTradePersistenceService>();

        builder.Services.AddHostedService<MarketOpenInterestSubscriber>();
        builder.Services.AddHostedService<MarketOpenInterestPersistenceService>();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .CreateBootstrapLogger();

        builder.Host.UseSerilog((context, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration) // Read from appsettings.json
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName));

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        var app = builder.Build();

        app.MapOpenApi();

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "v1");
        });

        app.MapControllers();

        await app.RunAsync();
    }
}
