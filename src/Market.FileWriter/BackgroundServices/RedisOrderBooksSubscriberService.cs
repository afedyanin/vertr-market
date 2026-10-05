using System.Threading.Channels;
using Market.Core.Models;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Market.FileWriter.BackgroundServices;

public class RedisOrderBooksSubscriberService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisOrderBooksSubscriberService> _logger;
    private readonly ISubscriber _subscriber;

    private readonly Channel<OrderBookDepth> _channel;

    public RedisOrderBooksSubscriberService(
        IConnectionMultiplexer redis,
        Channel<OrderBookDepth> channel,
        ILogger<RedisOrderBooksSubscriberService> logger)
    {
        _redis = redis;
        _logger = logger;
        _subscriber = _redis.GetSubscriber();
        _channel = channel;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var patternChannel = new RedisChannel("books:*", RedisChannel.PatternMode.Pattern);

        _logger.LogInformation($"Subscribing to channel pattern: {patternChannel}");

        await _redis.GetSubscriber().SubscribeAsync(patternChannel, (channel, value) =>
        {
            try
            {
                if (value.IsNullOrEmpty)
                {
                    return;
                }

                var orderBook = MessagePackSerializer.Deserialize<OrderBookDepth>(value, cancellationToken: stoppingToken);
                _channel.Writer.TryWrite(orderBook);
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
}
