using System.Collections.Concurrent;
using System.Threading.Channels;
using Market.Core.Abstractions;

namespace Market.Core.Events;

public class EventBroker<TMessage> : IEventBroker<TMessage>
{
    private readonly ConcurrentDictionary<Guid, ChannelWriter<TMessage>> _subscribers = new();

    public (Guid Id, ChannelReader<TMessage> Reader) Subscribe(ushort[] assets)
    {
        var id = Guid.NewGuid();

        var channel = Channel.CreateUnbounded<TMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        _subscribers.TryAdd(id, channel.Writer);
        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var writer))
        {
            writer.TryComplete();
        }
    }

    public void Publish(TMessage message)
    {
        foreach (var writer in _subscribers.Values)
        {
            writer.TryWrite(message);
        }
    }
}
