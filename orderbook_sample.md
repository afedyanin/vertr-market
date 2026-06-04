```csharp

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Disruptor;
using Disruptor.Dsl;

namespace UltraLowLatencyOrderBook;

#region 1. Модели Данных (Data Models)

// Элемент стакана (размер: 16 байт)
public readonly record struct OrderBookLevel(decimal Price, decimal Volume);

// Встроенный массив C# 12 (Inline Array) на 10 элементов.
// Предотвращает аллокацию массива в куче. Память выделяется прямо внутри структуры.
[InlineArray(10)]
public struct LevelBuffer
{
    private OrderBookLevel _element0;
}

// Основная структура стакана (размер: ~340 байт). Передается везде по ссылке (in / ref).
public struct OrderBook
{
    public int AssetId;
    public DateTime Timestamp;
    
    public LevelBuffer Bids; 
    public LevelBuffer Asks;
    
    public int BidCount;
    public int AskCount;
}

// Класс-контейнер события для Disruptor.
// Экземпляры создаются ОДИН РАЗ при старте внутри Ring Buffer и используются повторно.
public sealed class OrderBookEvent
{
    public OrderBook Value;
}

#endregion

#region 2. Издатель и Троттлер (Producer & Throttler)

public sealed class OrderBookThrottler
{
    // Хранилище актуальных стаканов по ID актива. Память выделяется один раз при заполнении.
    // При поступлении новых данных ячейки памяти просто перезаписываются.
    private readonly Dictionary<int, OrderBook> _latestBooks = new(1024);
    private readonly RingBuffer<OrderBookEvent> _ringBuffer;
    private readonly TimeSpan _interval;

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
        // Выделяем буфер под размер одной структуры OrderBook прямо в стеке
        Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<OrderBook>()];

        while (!ct.IsCancellationRequested)
        {
            // Читаем фиксированное количество байт из сети/диска
            int bytesRead = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (bytesRead == 0) break; // Стрим завершен

            // Интерпретируем байты как структуру OrderBook без копирования памяти
            ref readonly OrderBook incomingBook = ref MemoryMarshal.AsRef<OrderBook>(buffer);

            // Обновляем состояние в словаре
            HandleIncomingOrderBook(in incomingBook);
        }
    }

    /// <summary>
    /// Шаг 2: Обновление состояния по принципу "последний пришедший побеждает" (Zero-Allocation)
    /// </summary>
    public void HandleIncomingOrderBook(ref readonly OrderBook incomingBook)
    {
        // Находим или создаем структуру по ссылке прямо внутри внутреннего массива Dictionary
        ref OrderBook current = ref CollectionsMarshal.GetValueRefOrAddDefault(
            _latestBooks, incomingBook.AssetId, out bool _);
        
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
                long sequence = _ringBuffer.Next();
                try
                {
                    // Получаем пре-аллоцированный объект события по индексу
                    OrderBookEvent @event = _ringBuffer[sequence];
                    
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

#endregion

#region 3. Потребитель (Consumer)

public sealed class OrderBookDisruptorConsumer : IEventHandler<OrderBookEvent>
{
    /// <summary>
    /// Метод вызывается автоматически в выделенном потоке Disruptor.
    /// </summary>
    /// <param name="data">Ссылка на пре-аллоцированный объект в Ring Buffer</param>
    /// <param name="sequence">Порядковый номер события</param>
    /// <param name="endOfBatch">Флаг конца пачки (true, если это последнее доступное событие на данный момент)</param>
    public void OnEvent(OrderBookEvent data, long sequence, bool endOfBatch)
    {
        // Передаем структуру по ссылке (in), чтобы избежать копирования 340+ байт в стек метода
        ProcessSnapshot(in data.Value, endOfBatch);
        
        // Никаких вызовов Release() или возвратов в пул делать НЕ НУЖНО. 
        // Disruptor зациклит эту память автоматически, когда поток обработки пойдет на следующий круг.
    }

    private void ProcessSnapshot(in OrderBook book, bool endOfBatch)
    {
        // Чтение инлайн-массива через ReadOnlySpan (0 аллокаций)
        ReadOnlySpan<OrderBookLevel> bids = book.Bids;
        ReadOnlySpan<OrderBookLevel> asks = book.Asks;

        if (book.BidCount > 0 && book.AskCount > 0)
        {
            // Пример бизнес-логики: получаем лучшие цены (Спред)
            decimal bestBid = bids[0].Price;
            decimal bestAsk = asks[0].Price;
            
            // Здесь ваша логика: отправка по WebSocket клиентам, запись в БД и т.д.
            // Использование флага endOfBatch позволяет делать пакетную запись (Batching) для оптимизации IO.
        }
    }
}

#endregion

#region 4. Точка входа и Настройка инфраструктуры (Wiring)

public static class Program
{
    public static async Task Main()
    {
        // Размер буфера ДОЛЖЕН быть строго степенью двойки
        const int bufferSize = 1024; 

        // 1. Инициализируем Disruptor. Передаем фабрику для пре-аллокации наших классов-событий.
        var disruptor = new Disruptor<OrderBookEvent>(
            factory: () => new OrderBookEvent(), 
            bufferSize: bufferSize, 
            taskScheduler: TaskScheduler.Default, 
            producerType: ProducerType.Single, // У нас ровно один поток-издатель (таймер троттлера)
            waitStrategy: new YieldingWaitStrategy() // Оптимальный компромисс между задержкой и CPU
        );

        // 2. Регистрируем наш обработчик (Consumer)
        var consumer = new OrderBookDisruptorConsumer();
        disruptor.HandleEventsWith(consumer);

        // 3. Запускаем внутренние потоки Disruptor и получаем доступ к RingBuffer
        RingBuffer<OrderBookEvent> ringBuffer = disruptor.Start();

        // 4. Создаем Троттлер и передаем ему ссылку на RingBuffer
        var throttler = new OrderBookThrottler(TimeSpan.FromSeconds(5), ringBuffer);

        // Запуск фонового таймера отправки срезов (раз в 5 секунд)
        using var cts = new CancellationTokenSource();
        Task emittingTask = throttler.StartEmittingAsync(cts.Token);

        // Симуляция: Передаем пустой стрим для демонстрации парсинга (в реальности здесь будет NetworkStream/PipeReader)
        using var mockStream = new MemoryStream(); 
        await throttler.ParseStreamAsync(mockStream, cts.Token);

        // Корректное завершение при остановке приложения
        cts.Cancel();
        await emittingTask;
        disruptor.Shutdown();
    }
}

#endregion

```
## Архитектурные акценты этой реализации:

* [InlineArray(10)]: Полностью убирает накладные расходы на создание массивов OrderBookLevel[]. Данные лежат единым блоком памяти прямо внутри структуры OrderBook.
* CollectionsMarshal.GetValueRefOrAddDefault: Позволяет обновлять тяжелые структуры стаканов по месту (in-place) прямо в куче, внутри массива бакетов Dictionary.
* MemoryStream / Stream.ReadAsync(Span<byte>): Считывает бинарный кадр прямо в стек-память без выделения промежуточного массива байт new byte[size].
* Disruptor RingBuffer: Полностью заменяет менеджмент пулов объектов. Память под OrderBookEvent выделяется один раз при вызове disruptor.Start(). Потоки общаются через кэш-эффективную синхронизацию (Memory Barriers) без блокировок ядра ОС (Lock-free).


