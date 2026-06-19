using Disruptor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.Application.EventHandlers;
using Vertr.Market.Application.EventHandlers.Aggregated;
using Vertr.Market.Application.Models;
using Vertr.Market.Application.Publishers;

namespace Vertr.Market.Application;

public static class ApplicationRegistrar
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<IEventHandler<CandleEvent>, CandleDebugLogger>();
        services.AddTransient<IEventHandler<CandleEvent>, CandleSpreadCalculator>(
            sp => new CandleSpreadCalculator(
                sp.GetRequiredService<ISpreadPublisher>(),
                sp.GetRequiredService<ILogger<CandleSpreadCalculator>>(),
                inboundAssetId1: 200,
                inboundAssetId2: 300));

        services.AddSingleton<ICandlePublisher, CandlePublisher>();
        services.AddSingleton<IEventHandler<MarketTradeEvent>, TradeAggregatorByCandle>(
            sp => new TradeAggregatorByCandle(
                sp.GetRequiredService<ICandlePublisher>(),
                capacity: 128));

        services.AddTransient<ISpreadPublisher, SpreadDebugLogger>();

        return services;
    }
}
