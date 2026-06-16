using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;

namespace Vertr.Market.Application.Models;

public sealed class OrderBookThrottler
{
    private readonly object _lock = new();
    private readonly RingBuffer<OrderBookEvent> _ringBuffer;
    private readonly TimeSpan _interval;
    private readonly int _orderBookSize = Unsafe.SizeOf<OrderBook>();
    private readonly OrderBookEvent _accumulator = new();

    public OrderBookThrottler(TimeSpan interval, RingBuffer<OrderBookEvent> ringBuffer)
    {
        _interval = interval;
        _ringBuffer = ringBuffer;
    }

    public async ValueTask ParseStreamAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[_orderBookSize];
        var memoryBuffer = buffer.AsMemory();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await stream.ReadExactlyAsync(memoryBuffer, ct).ConfigureAwait(false);
                ref readonly var incomingBook = ref MemoryMarshal.AsRef<OrderBook>(buffer);

                if ((uint)incomingBook.AssetId >= OrderBookEvent.Capacity)
                {
                    // Логируем ошибку / пропускаем коррумпированный пакет
                    continue;
                }

                // Атомарно обновляем срез данных для таймера
                lock (_lock)
                {
                    _accumulator[incomingBook.AssetId] = incomingBook;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Нормальное завершение работы
        }
    }


    public async Task StartEmittingAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            // 1. Аллоцируем слот в RingBuffer силами консьюмера/таймера
            var sequence = _ringBuffer.Next();
            var targetEvent = _ringBuffer[sequence];

            // 2. Копируем накопленные за интервал стаканы под локом
            lock (_lock)
            {
                // Быстрое копирование всего массива структур (Блиц-перенос памяти)
                _accumulator.CopyTo(targetEvent);

                // Очищаем аккумулятор для следующего интервала времени
                _accumulator.Clear();
            }

            // 3. Публикуем событие в Disruptor для дальнейшей обработки
            _ringBuffer.Publish(sequence);
        }
    }
}
