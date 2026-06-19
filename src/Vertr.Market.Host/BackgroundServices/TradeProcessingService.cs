using System.Threading.Channels;
using Disruptor;
using Disruptor.Dsl;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.Application.Consumers;
using Vertr.Market.Application.EventHandlers;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Host.BackgroundServices;

public class TradeProcessingService : BackgroundService
{
    private static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RestartInterval = TimeSpan.FromSeconds(15);

#pragma warning disable CA1805 // Do not initialize unnecessarily
    private readonly bool _isEnabled = false;
#pragma warning restore CA1805 // Do not initialize unnecessarily
    private const string ServiceName = nameof(TradeProcessingService);

    private readonly Channel<Trade> _channel;
    private readonly ILogger<TradeProcessingService> _logger;

    private readonly Disruptor<MarketTradeEvent> _disruptor;
    private readonly RingBuffer<MarketTradeEvent> _ringBuffer;

    public TradeProcessingService(
        Channel<Trade> channel,
        ILogger<TradeProcessingService> logger)
    {
        _channel = channel;
        _logger = logger;

        _disruptor = new Disruptor<MarketTradeEvent>(
              () => new MarketTradeEvent(),
              ringBufferSize: 4096,
              TaskScheduler.Default,
              ProducerType.Single,
              new BlockingWaitStrategy());

        var publisher = new DummyCandlePublisher();
        var aggregator = new TradeAggregatorByCandle(publisher, PublishInterval);

        _disruptor.HandleEventsWith(aggregator);
        _ringBuffer = _disruptor.Start();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!_isEnabled)
            {
                _logger.LogWarning("{ServiceName} is disabled.", ServiceName);
                return;
            }

            await StartExecutingLoop(stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, ex.Message);
        }
        finally
        {
            _logger.LogInformation("Stopping Disruptor in {ServiceName}...", ServiceName);
            _disruptor.Halt();
        }

        _logger.LogInformation("{ServiceName} execution completed at {EndTime:O}", ServiceName, DateTime.UtcNow);
    }

    private async Task StartExecutingLoop(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("{ServiceName} started at {StartTime:O}", ServiceName, DateTime.UtcNow);
                var consumer = new TradeChannelConsumer(_ringBuffer, _channel, PublishInterval);
                await consumer.ExecuteAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ServiceName} exception. Message={Message}", ServiceName, ex.Message);
                await Task.Delay(RestartInterval, stoppingToken);
            }
        }
    }
}

public class DummyCandlePublisher : ICandleSnapshotPublisher
{
    public void Publish(in Candle candle)
    {
        Console.WriteLine($"Id={candle.AssetId} OpenTime={candle.OpenTime:O} O={candle.Open:F4} H={candle.High:F4} L={candle.Low:F4} C={candle.Close:F4} V={candle.Value} VL={candle.Value:F4}");
    }
}

