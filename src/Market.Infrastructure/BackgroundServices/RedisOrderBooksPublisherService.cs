using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Market.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Market.Infrastructure.BackgroundServices;

public class RedisOrderBooksPublisherService : BackgroundService
{
    private readonly ChannelReader<OrderBookDepth> _reader;
    private readonly ILogger<RedisOrderBooksPublisherService> _logger;
    private readonly IDatabase _redisDb;
    private readonly ConcurrentDictionary<ushort, RedisChannel> _channelCache = new();

    public RedisOrderBooksPublisherService(
        IConnectionMultiplexer redis,
        Channel<OrderBookDepth> channel,
        ILoggerFactory loggerFactory)
    {
        _reader = channel.Reader;
        _redisDb = redis.GetDatabase();
        _logger = loggerFactory.CreateLogger<RedisOrderBooksPublisherService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting order books redis publisher...");

        try
        {
            await foreach (var book in _reader.ReadAllAsync(stoppingToken))
            {
                var redisChannel = _channelCache.GetOrAdd(
                    book.AssetId,
                    symbol => new RedisChannel($"books:{symbol}", RedisChannel.PatternMode.Auto));

                //MessagePackSerializer.Serialize(book, cancellationToken: stoppingToken),

                await _redisDb.PublishAsync(
                    redisChannel,
                    JsonSerializer.Serialize(book, book.GetType()),
                    CommandFlags.FireAndForget);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order books redis publisher error. Message={Message}", ex.Message);
        }

        _logger.LogInformation("Order books redis publisher stopped.");
    }
}
