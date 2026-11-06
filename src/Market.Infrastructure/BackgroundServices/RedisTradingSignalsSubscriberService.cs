using System.Text.Json;
using System.Threading.Channels;
using Market.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Market.Infrastructure.BackgroundServices;

public class RedisTradingSignalsSubscriberService : BackgroundService
{
    private readonly ISubscriber _subscriber;
    private readonly ChannelWriter<IPairTradingSignal> _writer;
    private readonly ILogger<RedisTradingSignalsSubscriberService> _logger;

    public RedisTradingSignalsSubscriberService(
        IConnectionMultiplexer redis,
        Channel<IPairTradingSignal> channel,
        ILoggerFactory loggerFactory)
    {
        _subscriber = redis.GetSubscriber();
        _writer = channel.Writer;
        _logger = loggerFactory.CreateLogger<RedisTradingSignalsSubscriberService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var patternChannel = new RedisChannel("signals:*", RedisChannel.PatternMode.Pattern);

        _logger.LogInformation($"Subscribing to channel pattern: {patternChannel}");

        await _subscriber.SubscribeAsync(patternChannel, (channel, value) =>
        {
            try
            {
                if (!value.HasValue)
                {
                    return;
                }

                var signal = JsonSerializer.Deserialize<PairTradingSignal>((string)value!);
                _writer.TryWrite(signal!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis trading signals subscriber error. Message={Message}", ex.Message);
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
