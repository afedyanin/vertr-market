using System.Threading.Channels;
using Disruptor;
using Disruptor.Dsl;
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

        var publisher = new ConsolePublisher();
        var aggregator = new OrderBookAggregatorByLastItem(publisher, TimeSpan.FromSeconds(5));
        disruptor.HandleEventsWith(aggregator);
        var ringBuffer = disruptor.Start();

        var channel = Channel.CreateUnbounded<OrderBook>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var consumer = new OrderBookChannelConsumer(ringBuffer, channel.Reader);

        var writeTask = WriteOrderBooksAsync(channel, cts.Token);
        var parseTask = consumer.ExecuteAsync(cts.Token);
        var snapshotTask = aggregator.StartEmittingAsync(cts.Token);

        await Task.WhenAll(parseTask, writeTask, snapshotTask);
        disruptor.Shutdown();
    }

    private static async Task WriteOrderBooksAsync(Channel<OrderBook> channel, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await channel.Writer.WriteAsync(new OrderBook(), ct);
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

public class ConsolePublisher : IOrderBookSnapshotPublisher
{
    public async Task PublishAsync(IReadOnlyList<OrderBook> books, CancellationToken ct)
    {
        Console.WriteLine("new batch of books: ");

        foreach (var book in books)
        {
            Console.WriteLine(book);
        }

        await Task.CompletedTask;
    }
}
