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
        var assetXChannel = new RedisChannel($"books:{_assetX}", RedisChannel.PatternMode.Literal);
        await _subscriber.SubscribeAsync(assetXChannel,
            (channel, value) => HadleChannelMessages(value));

        var assetYChannel = new RedisChannel($"books:{_assetY}", RedisChannel.PatternMode.Literal);
        await _subscriber.SubscribeAsync(assetYChannel,
            (channel, value) => HadleChannelMessages(value));

        var processingTask = ProcessQueueAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await _subscriber.UnsubscribeAsync(assetXChannel);
            await _subscriber.UnsubscribeAsync(assetYChannel);

            _marketDataChannel.Writer.Complete();
            await processingTask;
        }
    }

    private void HadleChannelMessages(RedisValue value)
    {
        try
        {
            if (value.IsNullOrEmpty)
            {
                return;
            }

            var marketDepth = MessagePackSerializer
            .Deserialize<OrderBookDepth>(value);

            if (marketDepth is null)
            {
                return;
            }

            _marketDataChannel.Writer.TryWrite(marketDepth);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling channel message: {Message}", ex.Message);
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

                var signal = marketDepth.AssetId == _assetX
                        ? _algo.ProcessTickX(marketDepth, marketDepth.Timestamp)
                        : _algo.ProcessTickY(marketDepth, marketDepth.Timestamp);

                if (signal is null)
                {
                    continue;
                }

                await _redisDb.PublishAsync(
                    _signalsChannel,
                    MessagePackSerializer.Serialize(signal),
                    CommandFlags.FireAndForget);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing trading logic for Asset {AssetId}", marketDepth.AssetId);
            }
        }
    }
}
