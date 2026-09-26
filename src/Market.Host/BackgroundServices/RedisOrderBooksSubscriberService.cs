using Market.Core.Models;
using MessagePack;
using StackExchange.Redis;

namespace Market.Host.BackgroundServices;

public class RedisOrderBooksSubscriberService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisOrderBooksSubscriberService> _logger;
    private readonly ISubscriber _subscriber;

    public RedisOrderBooksSubscriberService(
        IConnectionMultiplexer redis,
        ILogger<RedisOrderBooksSubscriberService> logger)
    {
        _redis = redis;
        _logger = logger;
        _subscriber = _redis.GetSubscriber();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var patternChannel = new RedisChannel("books:*", RedisChannel.PatternMode.Pattern);

        _logger.LogInformation($"Subscribing to channel pattern: {patternChannel}");

        await _subscriber.SubscribeAsync(patternChannel, (channel, value) =>
        {
            try
            {
                if (value.IsNullOrEmpty)
                {
                    return;
                }

                var marketDepth = MessagePackSerializer.Deserialize<OrderBookDepth>(value, cancellationToken: stoppingToken);
                ProcessMarketDepth(channel, marketDepth);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Redis Subscriber Error parsing message]: {ex.Message}");
            }
        });

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Unsubscribing from Redis channels...");
            await _subscriber.UnsubscribeAsync(patternChannel);
        }
    }

    private void ProcessMarketDepth(RedisChannel channel, OrderBookDepth marketDepth)
    {
        // Ваша логика обработки полученного стакана цен
        _logger.LogDebug($"Received order book for Asset: {marketDepth.AssetId} from channel: {channel}");

        // Например, можно передать данные дальше во внутренний Channel или вызвать другой сервис
    }
}
