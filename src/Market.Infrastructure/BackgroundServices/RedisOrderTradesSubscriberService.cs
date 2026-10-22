using System.Text.Json;
using System.Threading.Channels;
using Market.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Market.Infrastructure.BackgroundServices;

public class RedisOrderTradesSubscriberService : BackgroundService
{
    private readonly ISubscriber _subscriber;
    private readonly ChannelWriter<MyTrade> _writer;
    private readonly ILogger<RedisOrderTradesSubscriberService> _logger;

    public RedisOrderTradesSubscriberService(
        IConnectionMultiplexer redis,
        Channel<MyTrade> channel,
        ILoggerFactory loggerFactory)
    {
        _subscriber = redis.GetSubscriber();
        _writer = channel.Writer;
        _logger = loggerFactory.CreateLogger<RedisOrderTradesSubscriberService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var patternChannel = new RedisChannel("trades:*", RedisChannel.PatternMode.Pattern);

        _logger.LogInformation($"Subscribing to channel pattern: {patternChannel}");

        await _subscriber.SubscribeAsync(patternChannel, (channel, value) =>
        {
            try
            {
                if (value.IsNullOrEmpty)
                {
                    return;
                }

                //var trade = MessagePackSerializer.Deserialize<MyTrade>(value, cancellationToken: stoppingToken);
                var trade = JsonSerializer.Deserialize<MyTrade>((string)value!);
                _writer.TryWrite(trade!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis order trades subscriber error. Message={Message}", ex.Message);
            }
        });

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Unsubscribing from order trades redis channel...");
            await _subscriber.UnsubscribeAsync(patternChannel);
        }
    }
}
