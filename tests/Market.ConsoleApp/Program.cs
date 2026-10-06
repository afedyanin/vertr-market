using System.Threading.Channels;
using Market.ConsoleApp.BackgroundServices;
using Market.Core.Models;
using Market.Core.PairTrading;
using Market.Infrastructure.BackgroundServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using StackExchange.Redis;

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

                // Hosted Service
                services.AddSingleton(sp => Channel.CreateUnbounded<OrderBookDepth>());
                services.AddHostedService<RedisOrderBooksSubscriberService>();
                services.AddHostedService<DumpOrderBooksService>();

                //services.AddSingleton(sp => Channel.CreateUnbounded<PairTradingSignal>());
                //services.AddHostedService<RedisTradingSignalsPublisherService>();
                //services.AddHostedService<DumpTradingSignalsService>();
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

    static void HftVwapEngineSample()
    {
        // Инициализируем робота. Задаем объем, который мы хотим торговать по каждому активу
        double tradeVolumeY = 5.0; // Например, 5 лотов/монет Asset Y
        double initialBeta = 1.5;
        // Объем для X будет динамически рассчитываться как: tradeVolumeY / Beta

        var engine = new HftVwapEngine(initialBeta: initialBeta, tradeVolumeY: tradeVolumeY);

        // Имитируем глубокий стакан (L2) для Актива X (Цена, Объем)
        var orderBookX = new OrderBookDepth(
            DateTime.UtcNow,
            1,
            bids: [new(100.0, 2.0), new(99.9, 4.0), new(99.8, 10.0)],
            asks: [new(100.1, 1.5), new(100.2, 3.5), new(100.3, 8.0)]
        );

        // Имитируем глубокий стакан (L2) для Актива Y (Цена, Объем)
        var orderBookY = new OrderBookDepth(
            DateTime.UtcNow,
            2,
            bids: [new(150.0, 1.0), new(149.8, 3.0), new(149.5, 5.0)],
            asks: [new(150.2, 2.0), new(150.4, 4.0), new(150.6, 6.0)]
        );

        // Тестируем расчет VWAP и фильтрацию
        var r = engine.ProcessSecond(orderBookX, orderBookY);

        Console.WriteLine($"Текущая Beta: {r.Beta:F3}");
        Console.WriteLine($"Требуемый объем Y: {tradeVolumeY} лотов. Реальный VWAP Ask Y: {r.VwapAskY:F2}, Bid Y: {r.VwapBidY:F2}");
        Console.WriteLine($"Требуемый объем X: {tradeVolumeY / r.Beta:F2} лотов. Реальный VWAP Ask X: {r.VwapAskX:F2}, Bid X: {r.VwapBidX:F2}");
        Console.WriteLine($"-----------------------------------------------------------------------------------------");
        Console.WriteLine($"Z-Score Buy (Вход в лонг Y): {r.ZScoreBuy:F2}");
        Console.WriteLine($"Z-Score Sell (Вход в шорт Y): {r.ZScoreSell:F2}");
        Console.WriteLine($"Сигнал: {r.Signal}");
    }

    static void HftFeesEngineSample()
    {
        // Настройки комиссий: например, 0.04% (0.0004) для Актива X и 0.05% (0.0005) для Актива Y
        double feeRateX = 0.0004;
        double feeRateY = 0.0005;
        double tradeVolumeY = 5.0;

        var engine = new HftFeesEngine(initialBeta: 1.5, tradeVolumeY: tradeVolumeY, feeRateX: feeRateX, feeRateY: feeRateY);

        // Стакан X (Цена, Объем)
        var orderBookX = new OrderBookDepth(
            DateTime.UtcNow,
            1,
            bids: [new(100.0, 10.0)],
            asks: [new(100.1, 10.0)]
        );

        // Стакан Y: Симулируем ситуацию, где спред расширился (Z-Score высокий), 
        // но из-за комиссий алгоритм должен проигнорировать этот вход
        var orderBookY = new OrderBookDepth(
            DateTime.UtcNow,
            2,
            bids: [new(151.2, 10.0)],
            asks: [new(151.5, 10.0)]
        );

        var r = engine.ProcessSecond(orderBookX, orderBookY);

        Console.WriteLine($"Чистый Z-Score Sell (Без комиссий): {r.RawZScoreSell:F2}");
        Console.WriteLine($"Скорректированный Z-Score Sell (С учетом комиссий): {r.ZScoreSellWithFees:F2}");
        Console.WriteLine($"Суммарная стоимость комиссий на круг (в единицах Y): {r.TotalFeesInY:F4}");
        Console.WriteLine($"Сигнал: {r.Signal}");
    }
}
