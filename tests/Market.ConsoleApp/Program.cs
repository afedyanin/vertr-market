using System.Threading.Channels;
using Market.BacktestEngine.BackgroundServices;
using Market.Core.Models;
using Market.Infrastructure.BackgroundServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using StackExchange.Redis;

namespace Market.BacktestEngine;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args) // MarketApiSettings:AssetId
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        try
        {
            Log.Information("Starting host...");

            var builder = Host.CreateDefaultBuilder(args);

            builder.ConfigureServices((context, services) =>
            {
                // Redis consumers
                var redisConnectionString = configuration.GetConnectionString("Redis");
                services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnectionString!));

                // Hosted Service
                services.AddSingleton(sp => Channel.CreateUnbounded<OrderBookDepth>());
                services.AddHostedService<RedisOrderBooksSubscriberService>();
                services.AddHostedService<DumpOrderBooksService>();

                //services.AddSingleton(sp => Channel.CreateUnbounded<PairTradingSignal>());
                //services.AddHostedService<RedisTradingSignalsPublisherService>();
                //services.AddHostedService<DumpTradingSignalsService>();
            });

            builder.UseEnvironment(environment);
            builder.UseSerilog();

            using var host = builder.Build();

            await host.RunAsync();

            Log.Information("Stopping host...");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host fatal exception. Message={Message}", ex.Message);
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
