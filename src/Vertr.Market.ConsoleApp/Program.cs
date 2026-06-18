using System.Threading.Channels;
using Disruptor;
using Disruptor.Dsl;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.Application.Consumers;
using Vertr.Market.Application.EventHandlers;
using Vertr.Market.Application.Models;

namespace Vertr.Market.ConsoleApp;

public static class Program
{
    public static async Task Main()
    {
        await StartOrderBooksProcessing();
    }

    public static async Task StartOrderBooksProcessing()
    {
        var disruptor = new Disruptor<OrderBookEvent>(
            () => new OrderBookEvent(),
            ringBufferSize: 4096,
            TaskScheduler.Default,
            ProducerType.Single,
            new YieldingWaitStrategy()
        );

        var interval = TimeSpan.FromSeconds(5);
        var publisher = new DummyPublisher();
        var aggregator = new OrderBookAggregatorByLastItem(publisher, interval);
        disruptor.HandleEventsWith(aggregator);
        var ringBuffer = disruptor.Start();

        var channel = Channel.CreateUnbounded<OrderBook>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var consumer = new OrderBookChannelConsumer(ringBuffer, channel.Reader, interval);

        var writeTask = WriteOrderBooksAsync(channel, cts.Token);
        var parseTask = consumer.ExecuteAsync(cts.Token);

        await Task.WhenAll(parseTask, writeTask);
        disruptor.Shutdown();
    }

    private static async Task WriteOrderBooksAsync(Channel<OrderBook> channel, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await channel.Writer.WriteAsync(
                    new OrderBook()
                    {
                        AssetId = Random.Shared.Next(0, 100),
                        Timestamp = DateTime.UtcNow
                    }, ct);

                await Task.Delay(100, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            channel.Writer.Complete();
        }
    }
}

public class DummyPublisher : IOrderBookSnapshotPublisher
{
    public void Publish(in OrderBook orderBook)
    {
        Console.WriteLine($"AssetId={orderBook.AssetId} Timestamp:{orderBook.Timestamp:O}");
    }
}
