using System.Collections.Concurrent;
using System.Threading.Channels;
using Market.Core.Models;
using MessagePack;
using StackExchange.Redis;

namespace Market.Gateways.Tinvest.BackgroundServices;

public class RedisAggregatedTradesPublisherService : BackgroundService
{
    private readonly ChannelReader<Candle> _barChannel;
    private readonly ILogger<RedisAggregatedTradesPublisherService> _logger;
    private readonly IDatabase _redisDb;
    private readonly ConcurrentDictionary<ushort, RedisChannel> _channelCache = new();

    public RedisAggregatedTradesPublisherService(
        IServiceProvider serviceProvider,
         IConnectionMultiplexer redis,
        ILogger<RedisAggregatedTradesPublisherService> logger)
    {
        _barChannel = serviceProvider.GetRequiredService<Channel<Candle>>();
        _redisDb = redis.GetDatabase();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var bar in _barChannel.ReadAllAsync(stoppingToken))
            {
                var redisChannel = _channelCache.GetOrAdd(
                    bar.AssetId,
                    symbol => new RedisChannel($"bars:{symbol}", RedisChannel.PatternMode.Auto));

                await _redisDb.PublishAsync(
                    redisChannel,
                    MessagePackSerializer.Serialize(bar, cancellationToken: stoppingToken),
                    CommandFlags.FireAndForget
                );
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Redis Publisher Error]: {ex.Message}");
        }
    }
}
