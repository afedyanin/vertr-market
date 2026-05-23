using Disruptor;
using Vertr.Market.Application.Abstractions;

namespace Vertr.Market.Application.Models;

public class GenericMarketDataManager<T, TEvent>
    where T : class, IResetable<T>, new()
    where TEvent : class, new()
{
    private readonly RingBuffer<TEvent> _ringBuffer;
    private readonly int _arraySize;
    private readonly AtomicCell<T>[] _sharedStorage;
    private readonly Func<TEvent, T[]> _arrayExtractor;

    public GenericMarketDataManager(
        RingBuffer<TEvent> ringBuffer,
        int arraySize,
        Action<TEvent, int> eventInitializer,
        Func<TEvent, T[]> arrayExtractor)
    {
        _ringBuffer = ringBuffer;
        _arraySize = arraySize;
        _arrayExtractor = arrayExtractor;
        _sharedStorage = new AtomicCell<T>[_arraySize];

        for (var i = 0; i < _arraySize; i++)
        {
            _sharedStorage[i] = new AtomicCell<T>();
        }

        for (long i = 0; i < _ringBuffer.BufferSize; i++)
        {
            var @event = _ringBuffer[i];
            eventInitializer(@event, _arraySize);
        }
    }

    public AtomicCell<T> GetCell(int index)
        => _sharedStorage[index];

    public async Task PublishingLoopAsync(TimeSpan interval, CancellationToken token)
    {
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                var sequence = _ringBuffer.Next();

                try
                {
                    var targetEvent = _ringBuffer[sequence];
                    var destinationArray = _arrayExtractor(targetEvent);
                    CopySnapshot(_sharedStorage, destinationArray);
                }
                finally
                {
                    _ringBuffer.Publish(sequence);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private void CopySnapshot(AtomicCell<T>[] source, T[] destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            var cell = source[i];
            var destItemInEvent = destination[i];

            if (cell.TrySwapAndGetFilled(out var filledBuffer))
            {
                destItemInEvent.CopyFrom(filledBuffer!);
                cell.ClearBuffer(filledBuffer!);
            }
            else
            {
                destItemInEvent.IsEmpty = true;
            }
        }
    }
}