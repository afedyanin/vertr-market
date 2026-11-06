using System.Threading.Channels;
using Market.Core.Models;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Market.Infrastructure.BackgroundServices;

public class RedisOrderBooksSubscriberService : BackgroundService
{
    private readonly ISubscriber _subscriber;
    private readonly ChannelWriter<OrderBookDepth> _writer;
    private readonly ILogger<RedisOrderBooksSubscriberService> _logger;

    public RedisOrderBooksSubscriberService(
        IConnectionMultiplexer redis,
        Channel<OrderBookDepth> channel,
        ILoggerFactory loggerFactory)
    {
        _subscriber = redis.GetSubscriber();
        _writer = channel.Writer;
        _logger = loggerFactory.CreateLogger<RedisOrderBooksSubscriberService>();
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

                var orderBook = MessagePackSerializer.Deserialize<OrderBookDepth>(value, cancellationToken: stoppingToken);

                _writer.TryWrite(orderBook);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis order books subscriber error. Message={Message}", ex.Message);
            }
        });

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Unsubscribing from order books redis channel...");
            await _subscriber.UnsubscribeAsync(patternChannel);
        }
    }
}
