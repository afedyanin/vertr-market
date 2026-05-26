using Disruptor;
using Disruptor.Dsl;

namespace Vertr.Market.Palyground.Tests.Disruptor;

[TestFixture(Category = "Unit")]
public class DisruptorTests
{
    [Test]
    public void CanHandleEvent()
    {
        var disruptor = new Disruptor<SampleEvent>(() => new SampleEvent(), ringBufferSize: 1024);
        disruptor.HandleEventsWith(new SampleEventHandler());
        disruptor.Start();

        using (var scope = disruptor.PublishEvent())
        {
            var data = scope.Event();
            data.Id = 42;
            data.Value = 1.1;
        }

        Assert.Pass();
    }
}

public class SampleEvent
{
    public int Id { get; set; }
    public double Value { get; set; }
}

public class SampleEventHandler : IEventHandler<SampleEvent>
{
    public void OnEvent(SampleEvent data, long sequence, bool endOfBatch)
    {
        Console.WriteLine($"Event: {data.Id} => {data.Value}");
    }
}
