
using Market.Gateways.Tinvest.BackgroundServices;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;
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

        // Add Tinvest API
        builder.Services.AddOptions<TinvestSettings>().BindConfiguration(nameof(TinvestSettings));
        builder.Services.AddInvestApiClient((_, settings) => configuration.Bind($"{nameof(TinvestSettings)}:{nameof(InvestApiSettings)}", settings));
        builder.Services.AddHostedService<TinvestBackgroundService>();

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
