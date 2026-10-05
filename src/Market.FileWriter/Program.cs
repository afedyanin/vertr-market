using System.Threading.Channels;
using Market.Core.Models;
using Market.FileWriter.BackgroundServices;
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
                services.AddHostedService<RedisOrderBooksSubscriberService>();


                // Order Book Channel
                var channelOptions = new BoundedChannelOptions(capacity: 10000)
                {
                    FullMode = BoundedChannelFullMode.DropOldest, // Или Wait, если нельзя терять данные
                    SingleWriter = true,  // Подписка Redis работает в один поток на обработчик
                    SingleReader = true   // Наш фоновый метод чтения файла тоже один
                };

                var channel = Channel.CreateBounded<OrderBookDepth>(channelOptions);
                services.AddSingleton<Channel<OrderBookDepth>>();

                services.AddHostedService<OrderBookFileWriterService>();

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
