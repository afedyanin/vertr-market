using System.Threading.Channels;

namespace Market.Core.Abstractions;

public interface IEventBroker<TMessage>
{
    public (Guid Id, ChannelReader<TMessage> Reader) Subscribe(ushort[] assets);

    public void Unsubscribe(Guid id);

    public void Publish(TMessage message);
}
