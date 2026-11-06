using System.Threading.Channels;
using Market.Core.Models;
using Market.FileWriter.BackgroundServices;
using Market.Infrastructure.BackgroundServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using StackExchange.Redis;

namespace Market.FileWriter;

public static class Program
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

                services.AddSingleton(sp => Channel.CreateUnbounded<OrderBookDepth>(
                    new UnboundedChannelOptions()
                    {
                        SingleWriter = true, // RedisOrderBooksSubscriberService
                        SingleReader = true, // PairTradingStrategyService
                    }));

                // Hosted services
                services.AddHostedService<RedisOrderBooksSubscriberService>();
                services.AddHostedService<OrderBooksFileWriterService>();
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
