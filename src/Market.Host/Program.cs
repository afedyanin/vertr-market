using System.Diagnostics;
using System.Diagnostics.Metrics;
using Market.ApiClient;
using Market.Core;
using Market.Host.BackgroundServices;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

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
                .AddConsoleExporter()
                // Для отправки в Jaeger, Prometheus Agent, Grafana Tempo, Aspecto и др. по OTLP:
                // tracing.AddOtlpExporter(options =>
                // {
                // options.Endpoint = new Uri("http://localhost:4317"); // Адрес OTel Collector
                // })
                )
            .WithMetrics(metricsBuilder => metricsBuilder
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(serviceName)
                .AddPrometheusExporter()
                .AddConsoleExporter());

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        var settings = new MarketApiSettings();
        configuration.GetSection("MarketApiSettings").Bind(settings);

        builder.Services.AddOptions<MarketApiSettings>().BindConfiguration(nameof(MarketApiSettings));
        builder.Services.AddObjectStores();
        builder.Services.AddHostedService<TcpServer>();

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
