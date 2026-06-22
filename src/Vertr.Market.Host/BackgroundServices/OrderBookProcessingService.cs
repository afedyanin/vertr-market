using System.Threading.Channels;
using Disruptor;
using Disruptor.Dsl;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.Application.Consumers;
using Vertr.Market.Application.EventHandlers;
using Vertr.Market.Application.OrderBooks;

namespace Vertr.Market.Host.BackgroundServices;

public class OrderBookProcessingService : BackgroundService
{
    private static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RestartInterval = TimeSpan.FromSeconds(15);

    private readonly bool _isEnabled = true;
    private const string ServiceName = nameof(OrderBookProcessingService);

    private readonly Channel<OrderBook> _channel;
    private readonly ILogger<OrderBookProcessingService> _logger;

    private readonly Disruptor<OrderBookEvent> _disruptor;
    private readonly RingBuffer<OrderBookEvent> _ringBuffer;

    public OrderBookProcessingService(
        Channel<OrderBook> channel,
        ILogger<OrderBookProcessingService> logger)
    {
        _channel = channel;
        _logger = logger;

        _disruptor = new Disruptor<OrderBookEvent>(
              () => new OrderBookEvent(),
              ringBufferSize: 4096,
              TaskScheduler.Default,
              ProducerType.Single,
              new BlockingWaitStrategy());

        var publisher = new DummyBookPublisher();
        var aggregator = new OrderBookAggregatorByLastItem(publisher);

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
                var consumer = new OrderBookChannelConsumer(_ringBuffer, _channel, PublishInterval);
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

public class DummyBookPublisher : IOrderBookSnapshotPublisher
{
    public void Publish(in OrderBook orderBook)
    {
        Console.WriteLine($"OB Id={orderBook.AssetId} Time={orderBook.Timestamp:O}");
    }
}

