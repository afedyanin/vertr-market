using System.Threading.Channels;
using Market.Contracts.Models;
using Market.Core.PairTrading;
using MessagePack;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Market.Core.Strategies;

public class PairTradingStrategy
{
    private readonly ushort _assetX;
    private readonly ushort _assetY;
    private readonly IConnectionMultiplexer _redis;
    private readonly ISubscriber _subscriber;
    private readonly IDatabase _redisDb;
    private readonly RedisChannel _signalsChannel;
    private readonly ILogger<PairTradingStrategy> _logger;
    private readonly HftVwapEngineDeltaTime _algo;

    private readonly Channel<OrderBookDepth> _marketDataChannel;

    public PairTradingStrategy(
        ushort assetX,
        ushort assetY,
        IConnectionMultiplexer redis,
        ILoggerFactory loggerFactory)
    {
        _assetX = assetX;
        _assetY = assetY;
        _redis = redis;
        _subscriber = _redis.GetSubscriber();
        _redisDb = _redis.GetDatabase();
        _signalsChannel = new RedisChannel($"signals:{_assetX}:{_assetY}", RedisChannel.PatternMode.Auto);
        _logger = loggerFactory.CreateLogger<PairTradingStrategy>();
        _algo = new HftVwapEngineDeltaTime();

        _marketDataChannel = Channel.CreateUnbounded<OrderBookDepth>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO: Limit pattern to asset X,Y only
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

                var marketDepth = MessagePackSerializer
                .Deserialize<OrderBookDepth>(value, cancellationToken: stoppingToken);

                _marketDataChannel.Writer.TryWrite(marketDepth);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Redis Subscriber Error parsing message]: {ex.Message}");
            }
        });

        var processingTask = ProcessQueueAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            await _subscriber.UnsubscribeAsync(patternChannel);
        }
        finally
        {
            _marketDataChannel.Writer.Complete();
            await _subscriber.UnsubscribeAsync(patternChannel);
            await processingTask;
        }
    }

    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        await foreach (var marketDepth in _marketDataChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                if (marketDepth.AssetId != _assetX &&
                    marketDepth.AssetId != _assetY)
                {
                    continue;
                }

                // TODO: Use timestamp from OrderBook
                var currentTimestamp = DateTime.UtcNow;

                var signal = marketDepth.AssetId == _assetX
                        ? _algo.ProcessTickX(marketDepth, currentTimestamp)
                        : _algo.ProcessTickY(marketDepth, currentTimestamp);

                if (signal is null)
                {
                    continue;
                }

                await _redisDb.PublishAsync(
                    _signalsChannel,
                    MessagePackSerializer.Serialize(signal, cancellationToken: stoppingToken),
                    CommandFlags.FireAndForget);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing trading logic for Asset {AssetId}", marketDepth.AssetId);
            }
        }
    }
}
