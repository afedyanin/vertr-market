using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Market.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Market.Infrastructure.BackgroundServices;

public class RedisMyTradesPublisherService : BackgroundService
{
    private readonly ChannelReader<MyTrade> _reader;
    private readonly ILogger<RedisMyTradesPublisherService> _logger;
    private readonly IDatabase _redisDb;
    private readonly ConcurrentDictionary<ushort, RedisChannel> _channelCache = new();

    public RedisMyTradesPublisherService(
        IConnectionMultiplexer redis,
        Channel<MyTrade> channel,
        ILoggerFactory loggerFactory)
    {
        _reader = channel.Reader;
        _redisDb = redis.GetDatabase();
        _logger = loggerFactory.CreateLogger<RedisMyTradesPublisherService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting order trades redis publisher...");

        try
        {
            await foreach (var trade in _reader.ReadAllAsync(stoppingToken))
            {
                var redisChannel = _channelCache.GetOrAdd(
                    trade.AssetId,
                    symbol => new RedisChannel($"trades:{symbol}", RedisChannel.PatternMode.Auto));

                await _redisDb.PublishAsync(
                    redisChannel,
                    JsonSerializer.Serialize(trade, trade.GetType()),
                    CommandFlags.FireAndForget);

                /*
                await _redisDb.PublishAsync(
                    redisChannel,
                    MessagePackSerializer.Serialize(trade, cancellationToken: stoppingToken),
                    CommandFlags.FireAndForget);
                */
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order trades redis publisher error. Message={Message}", ex.Message);
        }

        _logger.LogInformation("Order trades redis publisher stopped.");
    }
}
