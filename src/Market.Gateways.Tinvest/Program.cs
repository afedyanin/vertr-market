using System.Threading.Channels;
using Market.Core.Models;
using Market.Core.Repositories;
using Market.Gateways.Tinvest.BackgroundServices;
using Market.Infrastructure;
using Market.Infrastructure.BackgroundServices;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;
using StackExchange.Redis;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;

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


        builder.Services.AddSingleton(sp => Channel.CreateUnbounded<OrderBookDepth>(
            new UnboundedChannelOptions()
            {
                SingleWriter = true, // OrderBooksStreamService
                SingleReader = true, // RedisOrderBooksPublisherService
            }));

        builder.Services.AddSingleton(sp => Channel.CreateUnbounded<MyTrade>(
            new UnboundedChannelOptions()
            {
                SingleWriter = true, // OrderExecutionEngineService
                SingleReader = true, // RedisMyTradesPublisherService
            }));

        builder.Services.AddSingleton(sp => Channel.CreateUnbounded<IPairTradingSignal>(
            new UnboundedChannelOptions()
            {
                SingleWriter = true, // RedisTradingSignalsSubscriberService
                SingleReader = true, // OrderExecutionEngineService
            }));

        builder.Services.AddSingleton(sp => Channel.CreateUnbounded<OrderTrades>(
            new UnboundedChannelOptions()
            {
                SingleWriter = true, // OrderTradesStreamService
                SingleReader = true, // OrderExecutionEngineService
            }));

        // Add Tinvest API
        builder.Services.AddOptions<TinvestSettings>().BindConfiguration(nameof(TinvestSettings));
        builder.Services.AddInvestApiClient((_, settings) => configuration.Bind($"{nameof(TinvestSettings)}:{nameof(InvestApiSettings)}", settings));

        // Hosted services
        builder.Services.AddHostedService<OrderBooksStreamService>();
        builder.Services.AddHostedService<OrderTradesStreamService>();
        builder.Services.AddHostedService<OrderExecutionEngineService>();

        // Redis 
        var redisConnectionString = configuration.GetConnectionString("Redis");
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnectionString!));

        // Redis consumers & publishers
        builder.Services.AddHostedService<RedisOrderBooksPublisherService>();
        builder.Services.AddHostedService<RedisMyTradesPublisherService>();
        builder.Services.AddHostedService<RedisTradingSignalsSubscriberService>();

        // Repositories
        builder.Services.AddStaticDataProvider();

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

        // Load static data
        var options = app.Services.GetRequiredService<IOptions<TinvestSettings>>();
        var staticDataProvider = app.Services.GetRequiredService<IStaticDataProvider>();
        await staticDataProvider.LoadFrom(options.Value.StaticDataDirectory);

        await app.RunAsync();
    }
}
