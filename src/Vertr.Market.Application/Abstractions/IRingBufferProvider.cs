using Disruptor;

namespace Vertr.Market.Application.Abstractions;

public interface IRingBufferProvider<T> where T : class
{
    public RingBuffer<T> RingBuffer { get; }
}
