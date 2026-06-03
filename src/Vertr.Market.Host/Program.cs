using Serilog;
using Vertr.Market.Application;
using Vertr.Market.Host.BackgroundServices;

namespace Vertr.Market.Host;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var configuration = builder.Configuration;

        // add Logging
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .CreateBootstrapLogger();

        builder.Host.UseSerilog((context, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration) // Read from appsettings.json
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
            .Enrich.WithThreadId());

        // add Background services

        builder.Services.AddHostedService<MarketDataSnapshotService>();
        builder.Services.AddHostedService<SynteticDataGenerationService>();

        builder.Services.AddApplication();

        var app = builder.Build();

        await app.RunAsync();
    }
}
