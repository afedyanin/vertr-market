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
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new InvalidOperationException("Stream does not support reading.");
        }

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
        catch (OperationCanceledException)
        {
            // Нормальное завершение работы
        }
        catch (IOException) when (ct.IsCancellationRequested)
        {
            // Сеть разорвана из-за отмены — не выбрасываем повторно
        }
        catch (ObjectDisposedException) when (ct.IsCancellationRequested)
        {
            // Stream был удалён из-за отмены — не выбрасываем повторно
        }
    }


    public async Task StartEmittingAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            try
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
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Disruptor может выбросить при переполнении RingBuffer или других ошибках.
                // При отмене — не логируем, так как это ожидаемое поведение.
                // TODO: добавить реальное логирование
            }
        }
    }
}
