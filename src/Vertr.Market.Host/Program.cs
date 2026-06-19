using System.Threading.Channels;
using Serilog;
using Vertr.Market.Application;
using Vertr.Market.Application.Models;
using Vertr.Market.Host.BackgroundServices;
using Vertr.Market.Tinvest;

namespace Vertr.Market.Host;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var configuration = builder.Configuration;

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .CreateBootstrapLogger();

        builder.Host.UseSerilog((context, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration) // Read from appsettings.json
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
            .Enrich.WithThreadId());


        builder.Services.AddSingleton(Channel.CreateUnbounded<OrderBook>());
        builder.Services.AddSingleton(Channel.CreateUnbounded<Trade>());
        builder.Services.AddApplication();
        builder.Services.AddTinvestMarketData(configuration);

        builder.Services.AddHostedService<TinvestMarketDataConsumerService>();
        builder.Services.AddHostedService<OrderBookProcessingService>();
        builder.Services.AddHostedService<TradeProcessingService>();

        var app = builder.Build();

        await app.RunAsync();
    }
}
