using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Market.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Market.Infrastructure.BackgroundServices;

public class RedisTradingSignalsPublisherService : BackgroundService
{
    private readonly ChannelReader<IPairTradingSignal> _reader;
    private readonly ILogger<RedisTradingSignalsPublisherService> _logger;
    private readonly IDatabase _redisDb;
    private readonly ConcurrentDictionary<uint, RedisChannel> _channelCache = new();

    public RedisTradingSignalsPublisherService(
        IConnectionMultiplexer redis,
        Channel<IPairTradingSignal> channel,
        ILoggerFactory loggerFactory)
    {
        _reader = channel.Reader;
        _redisDb = redis.GetDatabase();
        _logger = loggerFactory.CreateLogger<RedisTradingSignalsPublisherService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting trading signals redis publisher...");

        try
        {
            await foreach (var signal in _reader.ReadAllAsync(stoppingToken))
            {
                var redisChannel = _channelCache.GetOrAdd(
                    ((uint)signal.BookX.AssetId << 16) | signal.BookY.AssetId,
                    _ => new RedisChannel($"signals:{signal.BookX.AssetId}:{signal.BookY.AssetId}", RedisChannel.PatternMode.Auto));

                await _redisDb.PublishAsync(
                    redisChannel,
                    JsonSerializer.Serialize(signal, signal.GetType()),
                    CommandFlags.FireAndForget);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trading signals redis publisher error. Message={Message}", ex.Message);
        }

        _logger.LogInformation("Trading signals redis publisher stopped.");
    }
}
