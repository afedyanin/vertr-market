using StackExchange.Redis;
using Vertr.Common.Contracts;
using Vertr.Common.Contracts.Abstractions;
using static StackExchange.Redis.RedisChannel;

namespace Vertr.Market.Host.BackgroundServices;

public class MarketOrderBookSubscriber : RedisServiceBase
{
    private readonly ITimeKeyedLocalStorage _orderBookRepository;
    private readonly ILogger<MarketOrderBookSubscriber> _logger;

    protected override RedisChannel RedisChannel => new RedisChannel(Subscriptions.OrderBooks.Channel, PatternMode.Pattern);
    protected override bool IsEnabled => Subscriptions.OrderBooks.IsEnabled;

    public MarketOrderBookSubscriber(IServiceProvider serviceProvider, IConfiguration configuration) : base(serviceProvider, configuration)
    {
        _orderBookRepository = serviceProvider.GetRequiredService<ITimeKeyedLocalStorage>();
        _logger = LoggerFactory.CreateLogger<MarketOrderBookSubscriber>();
    }

    public override void HandleSubscription(RedisChannel channel, RedisValue message)
    {
        var orderBook = OrderBook.FromJson(message.ToString());

        if (orderBook == null)
        {
            _logger.LogWarning("Cannot deserialize Order Book from message={Message}", message);
            return;
        }

        _orderBookRepository.Update(orderBook);
        _logger.LogInformation("Received order book from cahnnel={Channel} OrderBook={OrderBook}", channel, orderBook);
    }
}
