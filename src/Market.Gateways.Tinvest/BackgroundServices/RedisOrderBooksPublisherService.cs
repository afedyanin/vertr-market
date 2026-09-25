using System.Collections.Concurrent;
using System.Threading.Channels;
using Market.ApiClient.Dtos;
using MessagePack;
using StackExchange.Redis;

namespace Market.Gateways.Tinvest.BackgroundServices;

public class RedisOrderBooksPublisherService : BackgroundService
{
    private readonly ChannelReader<MarketDepthDto> _marketDepthChannel;
    private readonly ILogger<RedisOrderBooksPublisherService> _logger;
    private readonly IDatabase _redisDb;
    private readonly ConcurrentDictionary<ushort, RedisChannel> _channelCache = new();

    public RedisOrderBooksPublisherService(
        IServiceProvider serviceProvider,
        IConnectionMultiplexer redis,
        ILogger<RedisOrderBooksPublisherService> logger)
    {
        _marketDepthChannel = serviceProvider.GetRequiredService<Channel<MarketDepthDto>>();
        _redisDb = redis.GetDatabase();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var bar in _marketDepthChannel.ReadAllAsync(stoppingToken))
            {
                var redisChannel = _channelCache.GetOrAdd(
                    bar.AssetId,
                    symbol => new RedisChannel($"books:{symbol}", RedisChannel.PatternMode.Auto));

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
