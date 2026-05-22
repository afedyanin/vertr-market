using Disruptor;

namespace Vertr.Market.Application.Abstractions;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
public class GenericMarketDataManager<T, TEvent>
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    where T : class, IResetable<T>, new()
    where TEvent : class, new()
{
    private readonly RingBuffer<TEvent> _ringBuffer;
    private readonly int _arraySize;
    private readonly CancellationTokenSource _cts = new();

    // Глобальное хранилище потокобезопасных ячеек
    private readonly AtomicCell<T>[] _sharedStorage;

    // Делегат для получения массива T[] из объекта TEvent
    private readonly Func<TEvent, T[]> _arrayExtractor;

    /// <param name="ringBuffer">Ссылка на RingBuffer Disruptor-а</param>
    /// <param name="arraySize">Размер массива элементов</param>
    /// <param name="eventInitializer">Действие для первоначального выделения памяти под массив внутри TEvent</param>
    /// <param name="arrayExtractor">Функция, возвращающая массив T[] из объекта TEvent</param>
    public GenericMarketDataManager(
        RingBuffer<TEvent> ringBuffer,
        int arraySize,
        Action<TEvent, int> eventInitializer,
        Func<TEvent, T[]> arrayExtractor)
    {
        _ringBuffer = ringBuffer;
        _arraySize = arraySize;
        _arrayExtractor = arrayExtractor;

        // Инициализируем общее хранилище
        _sharedStorage = new AtomicCell<T>[_arraySize];
        for (var i = 0; i < _arraySize; i++)
        {
            _sharedStorage[i] = new AtomicCell<T>();
        }

        // Заранее инициализируем массивы во ВСЕХ предвыделенных ивентах внутри пула Disruptor
        // (Это предотвращает аллокации памяти во время работы цикла)
        for (long i = 0; i < _ringBuffer.BufferSize; i++)
        {
            var @event = _ringBuffer[i];
            eventInitializer(@event, _arraySize);
        }
    }

    /// <summary>
    /// ВЫЗЫВАЕТСЯ РАБОЧИМИ ПОТОКАМИ.
    /// Запись любых кастомных классов T по индексу без блокировок.
    /// </summary>
    public void UpdateAtomically(int index, T incomingData)
    {
        _sharedStorage[index].UpdateData(incomingData);
    }

    /// <summary>
    /// Запуск бесконечного высокопроизводительного цикла публикации по таймеру.
    /// </summary>
    public void StartPublishingLoop(TimeSpan interval)
    {
        _ = Task.Run(() => PublishingLoopAsync(interval, _cts.Token), _cts.Token);
    }

    private async Task PublishingLoopAsync(TimeSpan interval, CancellationToken token)
    {
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                // Арендуем следующий свободный слот в кольцевом буфере
                var sequence = _ringBuffer.Next();
                try
                {
                    var targetEvent = _ringBuffer[sequence];
                    var destinationArray = _arrayExtractor(targetEvent);

                    // Извлекаем снимки и копируем их в событие Disruptor-а
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
                // Данные обновились: выполняем Zero-Allocation копирование свойств
                destItemInEvent.CopyFrom(filledBuffer!);

                // Очищаем внутренний буфер для следующего окна таймера
                cell.ClearBuffer(filledBuffer!);
            }
            else
            {
                // Изменений не было: мгновенно помечаем элемент в ивенте пустым.
                // Процессор не тратит время на копирование свойств.
                destItemInEvent.IsEmpty = true;
            }
        }
    }

    public void Stop() => _cts.Cancel();
}