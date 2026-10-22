using System.Threading.Channels;
using Market.Core.Models;
using Market.Gateways.Tinvest.BackgroundServices;
using Market.Infrastructure.BackgroundServices;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;
using StackExchange.Redis;
using Tinkoff.InvestApi;

namespace Market.Gateways.Tinvest;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var configuration = builder.Configuration;

        var otel = builder.Services.AddOpenTelemetry();

        otel.ConfigureResource(resource => resource
            .AddService(serviceName: builder.Environment.ApplicationName));

        otel.WithMetrics(metrics => metrics
            .AddPrometheusExporter()
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation());

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        var marketDepthChannel = Channel.CreateBounded<OrderBookDepth>(new BoundedChannelOptions(10000)
        {
            SingleWriter = true, // Писать в канал будет только gRPC поток
            SingleReader = true, // Читать будет один фоновый воркер записи
            FullMode = BoundedChannelFullMode.DropOldest // Если очередь полная, выкидываем старый стакан
        });

        builder.Services.AddSingleton(marketDepthChannel);

        var tradesChannel = Channel.CreateBounded<MyTrade>(new BoundedChannelOptions(1000)
        {
            SingleWriter = true,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        builder.Services.AddSingleton(tradesChannel);

        // Add Tinvest API
        builder.Services.AddOptions<TinvestSettings>().BindConfiguration(nameof(TinvestSettings));
        builder.Services.AddInvestApiClient((_, settings) => configuration.Bind($"{nameof(TinvestSettings)}:{nameof(InvestApiSettings)}", settings));
        builder.Services.AddHostedService<OrderBooksStreamService>();
        builder.Services.AddHostedService<OrderTradesStreamService>();

        // Redis publishers
        var redisConnectionString = configuration.GetConnectionString("Redis");
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnectionString!));
        builder.Services.AddHostedService<RedisOrderBooksPublisherService>();
        builder.Services.AddHostedService<RedisOrderTradesPublisherService>();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .CreateBootstrapLogger();

        builder.Host.UseSerilog((context, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration) // Read from appsettings.json
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
            .Enrich.WithThreadId());

        var app = builder.Build();

        app.MapPrometheusScrapingEndpoint();
        app.MapOpenApi();

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "v1");
        });

        app.MapControllers();

        await app.RunAsync();
    }
}
