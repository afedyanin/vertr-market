using StackExchange.Redis;
using Vertr.Common.Contracts;
using Vertr.Common.Contracts.Abstractions;
using static StackExchange.Redis.RedisChannel;

namespace Vertr.Market.Host.BackgroundServices;

public class OrderBookSubscriber : RedisServiceBase
{
    private readonly ITimeKeyedLocalStorage<OrderBook> _orderBookLocalStorage;
    private readonly ILogger<OrderBookSubscriber> _logger;

    protected override RedisChannel RedisChannel => new RedisChannel(Subscriptions.OrderBooks.Channel, PatternMode.Pattern);
    protected override bool IsEnabled => Subscriptions.OrderBooks.IsEnabled;

    public OrderBookSubscriber(IServiceProvider serviceProvider, IConfiguration configuration) : base(serviceProvider, configuration)
    {
        _orderBookLocalStorage = serviceProvider.GetRequiredService<ITimeKeyedLocalStorage<OrderBook>>();
        _logger = LoggerFactory.CreateLogger<OrderBookSubscriber>();
    }

    public override ValueTask HandleSubscription(RedisChannel channel, RedisValue message)
    {
        var orderBook = OrderBook.FromJson(message.ToString());

        if (orderBook == null)
        {
            _logger.LogWarning("Cannot deserialize Order Book from message={Message}", message);
            return ValueTask.CompletedTask;
        }

        if (orderBook.InstrumentId == Guid.Empty)
        {
            _logger.LogError("Order Book with empty InstrumentId received. Message={Message}", message);
            return ValueTask.CompletedTask;
        }

        var added = _orderBookLocalStorage.Add(orderBook.InstrumentId, orderBook);
        _logger.LogDebug("Received order book from cahnnel={Channel} Added={Added} OrderBook={OrderBook}", channel, added, orderBook);
        return ValueTask.CompletedTask;
    }
}
