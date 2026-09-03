using Market.ApiClient;
using Market.ConsoleApp.BackgroundServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Refit;
using Serilog;

namespace Market.ConsoleApp;

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
                services.AddRefitClient<IMarketRestApiClient>()
                    .ConfigureHttpClient((serviceProvider, client) =>
                    {
                        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                        var baseAddress = configuration["MarketApiSettings:BaseUrl"];

                        client.BaseAddress = new Uri(baseAddress ?? throw new InvalidOperationException("Market API BaseUrl is not configured."));
                    });

                services.AddHostedService<MarketInfoUpdaterService>();
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
