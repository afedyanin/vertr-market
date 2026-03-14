using StackExchange.Redis;
using Vertr.Common.Contracts;
using Vertr.Common.Contracts.Abstractions;
using static StackExchange.Redis.RedisChannel;

namespace Vertr.Market.Host.BackgroundServices;

public class MarketTradeSubscriber : RedisServiceBase
{
    private readonly ITimeKeyedLocalStorage<MarketTrade> _tradesLocalStorage;
    private readonly ILogger<MarketTradeSubscriber> _logger;

    protected override RedisChannel RedisChannel => new RedisChannel(Subscriptions.Trades.Channel, PatternMode.Pattern);
    protected override bool IsEnabled => Subscriptions.Trades.IsEnabled;

    public MarketTradeSubscriber(IServiceProvider serviceProvider, IConfiguration configuration) : base(serviceProvider, configuration)
    {
        _tradesLocalStorage = serviceProvider.GetRequiredService<ITimeKeyedLocalStorage<MarketTrade>>();
        _logger = LoggerFactory.CreateLogger<MarketTradeSubscriber>();
    }

    public override void HandleSubscription(RedisChannel channel, RedisValue message)
    {
        var marketTrade = MarketTrade.FromJson(message.ToString());

        if (marketTrade == null)
        {
            _logger.LogWarning("Cannot deserialize Market Trade from message={Message}", message);
            return;
        }

        var added = _tradesLocalStorage.Add(marketTrade.InstrumentId, marketTrade);
        _logger.LogInformation("Received Market Trade from cahnnel={Channel} Added={Added} Trade={OrderBook}", channel, added, marketTrade);
    }
}
