using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Market.Core.Models;
using Market.Host.BackgroundServices;
using Market.Infrastructure.BackgroundServices;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;
using Market.Infrastructure;

namespace Market.Host;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var configuration = builder.Configuration;
        var serviceName = builder.Environment.ApplicationName;
        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName);
        var hostActivitySource = new ActivitySource(serviceName);
        var hostMeter = new Meter(serviceName);

        builder.Services.AddSingleton(hostActivitySource);
        builder.Services.AddSingleton(hostMeter);

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracerBuilder => tracerBuilder
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(serviceName)
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri("http://localhost:4317");
                    options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                }))
            .WithMetrics(metricsBuilder => metricsBuilder
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddMeter(serviceName)
                .AddPrometheusExporter());

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .CreateBootstrapLogger();

        builder.Host.UseSerilog((context, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration) // Read from appsettings.json
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
            .Enrich.WithThreadId());

        // Channels
        builder.Services.AddSingleton(sp => Channel.CreateUnbounded<OrderBookDepth>(
            new UnboundedChannelOptions()
            {
                SingleWriter = true, // RedisOrderBooksSubscriberService
                SingleReader = true, // PairTradingStrategyService
            }));

        builder.Services.AddSingleton(sp => Channel.CreateUnbounded<MyTrade>(
            new UnboundedChannelOptions()
            {
                SingleWriter = true, // RedisMyTradesSubscriberService
                SingleReader = true, // PortfolioManagerService
            }));

        builder.Services.AddSingleton(sp => Channel.CreateUnbounded<PairTradingSignal>(
            new UnboundedChannelOptions()
            {
                SingleWriter = true, // PairTradingStrategyService
                SingleReader = true, // RedisTradingSignalsPublisherService
            }));

        // Repositories
        builder.Services.AddAlgoMetadataRepository();
        builder.Services.AddPortfolioRepository();
        builder.Services.AddStrategyRegistry();

        // Hosted services
        builder.Services.AddHostedService<PairTradingStrategyService>();
        builder.Services.AddHostedService<PortfolioManagerService>();

        // Redis
        var redisConnectionString = configuration.GetConnectionString("Redis");
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnectionString!));

        // Redis consumers & publishers
        builder.Services.AddHostedService<RedisOrderBooksSubscriberService>();
        builder.Services.AddHostedService<RedisMyTradesSubscriberService>();
        builder.Services.AddHostedService<RedisTradingSignalsPublisherService>();

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
