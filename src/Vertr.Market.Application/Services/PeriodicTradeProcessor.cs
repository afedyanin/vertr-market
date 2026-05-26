using Disruptor;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.Services;

public class PeriodicTradeProcessor : IPeriodicTradeProcessor
{
    private readonly TimeSpan _periodicInterval;
    private readonly int _arraySize;

    private readonly RingBuffer<PeriodicBarEvent> _ringBuffer;
    private readonly AtomicCell<Bar>[] _sharedStorage;

    public PeriodicTradeProcessor(RingBuffer<PeriodicBarEvent> ringBuffer, TimeSpan? interval = null, int instrumnentsCount = 1024)
    {
        _arraySize = instrumnentsCount; // количество обрабатываемых инструментов
        _periodicInterval = interval ?? TimeSpan.FromSeconds(1); // интервал генерации евента

        _ringBuffer = ringBuffer;
        _sharedStorage = new AtomicCell<Bar>[_arraySize];

        // В этот массив будет писать фид
        for (var i = 0; i < _arraySize; i++)
        {
            _sharedStorage[i] = new AtomicCell<Bar>();
        }

        // Этот массив периодически будет уходить в дисраптор
        for (long i = 0; i < _ringBuffer.BufferSize; i++)
        {
            var @event = _ringBuffer[i];
            @event.Bars = new Bar[_arraySize];
            for (var j = 0; j < _arraySize; j++)
            {
                @event.Bars[j] = new Bar();
            }
        }
    }

    public void HandleIncomingTrade(MarketTrade trade)
    {
        var index = trade.InstrumentId;
        var price = (double)trade.Price;
        var quantity = trade.Quantity;

        var cell = _sharedStorage[index];

        cell.UpdateInPlace(bar =>
        {
            bar.AggregateTrade(price, quantity);
        });
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        using var timer = new PeriodicTimer(_periodicInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var sequence = _ringBuffer.Next();

                try
                {
                    CopySnapshot(_sharedStorage, _ringBuffer[sequence].Bars);
                }
                finally
                {
                    _ringBuffer.Publish(sequence);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CopySnapshot(AtomicCell<Bar>[] source, Bar[] destination)
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