using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Market.Core.Models;

namespace Market.ConsoleApp.BackgroundServices;

public class RedisTradingSignalsSubscriberService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisTradingSignalsSubscriberService> _logger;
    private readonly ISubscriber _subscriber;

    public RedisTradingSignalsSubscriberService(
        IConnectionMultiplexer redis,
        ILogger<RedisTradingSignalsSubscriberService> logger)
    {
        _redis = redis;
        _logger = logger;
        _subscriber = _redis.GetSubscriber();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var patternChannel = new RedisChannel("signals:*", RedisChannel.PatternMode.Pattern);

        _logger.LogInformation($"Subscribing to channel pattern: {patternChannel}");

        await _subscriber.SubscribeAsync(patternChannel, (channel, value) =>
        {
            try
            {
                if (value.IsNullOrEmpty)
                {
                    return;
                }

                var signal = MessagePackSerializer.Deserialize<PairTradingSignal>(value, cancellationToken: stoppingToken);
                ProcessSignal(channel, signal);
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

    private void ProcessSignal(RedisChannel channel, PairTradingSignal signal)
    {
        _logger.LogDebug($"Received trading signal: {signal.Signal} from channel: {channel}");

        if (signal.Signal.Equals("HOLD", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _logger.LogInformation($"Signal details: {signal}");
    }
}
