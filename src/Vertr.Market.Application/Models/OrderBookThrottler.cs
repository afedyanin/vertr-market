using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;

namespace Vertr.Market.Application.Models;

public sealed class OrderBookThrottler
{
    // Хранилище актуальных стаканов по ID актива. Память выделяется один раз при заполнении.
    // При поступлении новых данных ячейки памяти просто перезаписываются.
    private readonly Dictionary<int, OrderBook> _latestBooks = new(1024);
    private readonly RingBuffer<OrderBookEvent> _ringBuffer;
    private readonly TimeSpan _interval;
    private readonly int _orderBookSize = Unsafe.SizeOf<OrderBook>();

    public OrderBookThrottler(TimeSpan interval, RingBuffer<OrderBookEvent> ringBuffer)
    {
        _interval = interval;
        _ringBuffer = ringBuffer;
    }

    /// <summary>
    /// Шаг 1: Быстрое чтение стаканов из бинарного стрима без аллокаций.
    /// </summary>
    public async ValueTask ParseStreamAsync(Stream stream, CancellationToken ct)
    {
        // Арендуем массив из пула (без аллокаций в куче)
        var rentArray = ArrayPool<byte>.Shared.Rent(_orderBookSize);
        // Отрезаем ровно столько, сколько занимает структура
        var memoryBuffer = rentArray.AsMemory(0, _orderBookSize);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Читаем фиксированное количество байт из сети/диска
                var bytesRead = await stream.ReadAsync(memoryBuffer, ct).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break; // Стрим завершен
                }

                // Интерпретируем байты как структуру OrderBook без копирования памяти
                ref readonly var incomingBook = ref MemoryMarshal.AsRef<OrderBook>(memoryBuffer.Span);

                // Обновляем состояние в словаре
                HandleIncomingOrderBook(in incomingBook);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentArray);
        }
    }

    /// <summary>
    /// Шаг 2: Обновление состояния по принципу "последний пришедший побеждает" (Zero-Allocation)
    /// </summary>
    public void HandleIncomingOrderBook(ref readonly OrderBook incomingBook)
    {
        // Находим или создаем структуру по ссылке прямо внутри внутреннего массива Dictionary
        ref var current = ref CollectionsMarshal.GetValueRefOrAddDefault(
            _latestBooks, incomingBook.AssetId, out var _);

        // Копируем входящую структуру в ячейку словаря (перезапись памяти)
        current = incomingBook;
    }

    /// <summary>
    /// Шаг 3: Периодический сброс (раз в 5 секунд) накопленных срезов в Disruptor
    /// </summary>
    public async Task StartEmittingAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            foreach (var kvp in _latestBooks)
            {
                // Запрашиваем следующий свободный индекс (Sequence) в кольцевом буфере.
                // Вызов может заблокировать поток, если буфер переполнен (зависит от WaitStrategy).
                var sequence = _ringBuffer.Next();
                try
                {
                    // Получаем пре-аллоцированный объект события по индексу
                    var @event = _ringBuffer[sequence];

                    // Копируем структуру стакана из словаря прямо в объект внутри буфера
                    @event.Value = kvp.Value;
                }
                finally
                {
                    // Публикуем событие — теперь оно доступно для Consumer
                    _ringBuffer.Publish(sequence);
                }
            }
        }
    }
}
