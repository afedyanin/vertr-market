using System.Threading.Channels;
using Disruptor;
using Disruptor.Dsl;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.Application.Consumers;
using Vertr.Market.Application.EventHandlers;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Host.BackgroundServices;

public class OrderBookProcessingService : BackgroundService
{
    private readonly Channel<OrderBook> _channel;

    public OrderBookProcessingService(Channel<OrderBook> channel)
    {
        _channel = channel;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var disruptor = new Disruptor<OrderBookEvent>(
            () => new OrderBookEvent(),
            ringBufferSize: 4096,
            TaskScheduler.Default,
            ProducerType.Single,
            new YieldingWaitStrategy()
        );

        // book pub interval
        var interval = TimeSpan.FromSeconds(5);

        // TODO: Move it in registrar
        var publisher = new DummyPublisher();
        var aggregator = new OrderBookAggregatorByLastItem(publisher, interval);
        disruptor.HandleEventsWith(aggregator);
        var ringBuffer = disruptor.Start();

        var consumer = new OrderBookChannelConsumer(ringBuffer, _channel, interval);

        await consumer.ExecuteAsync(stoppingToken);
        disruptor.Shutdown();
    }
}

public class DummyPublisher : IOrderBookSnapshotPublisher
{
    public void Publish(in OrderBook orderBook)
    {
        Console.WriteLine($"AssetId={orderBook.AssetId} Timestamp:{orderBook.Timestamp:O}");
    }
}

