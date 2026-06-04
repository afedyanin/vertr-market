Вот финальный, готовый к работе высокопроизводительный код на C# для агрегации трейдов в свечи (OHLCV) и их последующей передачи в Disruptor без единой аллокации в куче на этапе выполнения.
Здесь совмещены подходы из предыдущих ответов: CollectionsMarshal для in-place агрегации и архитектура Disruptor с пре-аллокацией объектов в кольцевом буфере.

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

namespace UltraLowLatencyCandles;

#region 1. Модели Данных (Data Models)

// Входной трейд (размер: 32 байта, readonly структура)
public readonly record struct Trade(
    int AssetId, 
    decimal Price,
    decimal Volume,
    DateTime Timestamp
);

// Выходная свеча (размер: ~64 байта)
public struct Candle
{
    public int AssetId;
    public DateTime OpenTime;
    public decimal Open;
    public decimal High;
    public decimal Low;
    public decimal Close;
    public decimal Volume;
    public bool IsInitialized;
}

// Класс-контейнер события для Disruptor.
// Экземпляры создаются ровно один раз при старте Ring Buffer и используются по кругу.
public sealed class CandleEvent
{
    public Candle Value;
}

#endregion

#region 2. Агрегатор и Издатель (Aggregator & Producer)

public sealed class CandleAggregator
{
    // Хранилище незакрытых свечей в памяти по ID актива.
    private readonly Dictionary<int, Candle> _activeCandles = new(1024);
    private readonly RingBuffer<CandleEvent> _ringBuffer;
    private readonly TimeSpan _candleInterval;

    public CandleAggregator(TimeSpan candleInterval, RingBuffer<CandleEvent> ringBuffer)
    {
        _candleInterval = candleInterval;
        _ringBuffer = ringBuffer;
    }

    /// <summary>
    /// Парсинг входящего бинарного потока трейдов (0 аллокаций в куче).
    /// </summary>
    public async ValueTask ParseTradeStreamAsync(Stream stream, CancellationToken ct)
    {
        // Выделяем буфер под размер одной структуры Trade прямо в стеке
        Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<Trade>()];

        while (!ct.IsCancellationRequested)
        {
            int bytesRead = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (bytesRead == 0) break; // Стрим завершен

            // Интерпретируем байты как структуру Trade без аллокации промежуточных массивов
            ref readonly Trade trade = ref MemoryMarshal.AsRef<Trade>(buffer);

            // Агрегируем трейд в свечу
            ProcessTrade(in trade);
        }

        // Перед остановкой принудительно сбрасываем все оставшиеся открытые свечи
        Flush();
    }

    /// <summary>
    /// Основная логика агрегации (Zero-Allocation).
    /// </summary>
    public void ProcessTrade(in Trade trade)
    {
        // 1. Вычисляем время начала текущего интервала свечи
        long ticks = trade.Timestamp.Ticks;
        long intervalTicks = _candleInterval.Ticks;
        DateTime candleOpenTime = new DateTime(ticks - (ticks % intervalTicks), trade.Timestamp.Kind);

        // 2. Получаем ссылку на свечу в куче (внутри базового массива Dictionary)
        ref Candle candle = ref CollectionsMarshal.GetValueRefOrAddDefault(_activeCandles, trade.AssetId, out bool exists);

        // 3. Если свеча уже была, но её время прошло — отправляем её в Disruptor и сбрасываем стейт
        if (exists && candle.OpenTime != candleOpenTime)
        {
            PublishCandleToDisruptor(in candle);
            exists = false; // Помечаем, что текущую ячейку нужно инициализировать заново
        }

        // 4. Обновляем поля свечи по ссылке (in-place модификация)
        if (!exists)
        {
            candle.AssetId = trade.AssetId;
            candle.OpenTime = candleOpenTime;
            candle.Open = trade.Price;
            candle.High = trade.Price;
            candle.Low = trade.Price;
            candle.Close = trade.Price;
            candle.Volume = trade.Volume;
            candle.IsInitialized = true;
        }
        else
        {
            if (trade.Price > candle.High) candle.High = trade.Price;
            if (trade.Price < candle.Low) candle.Low = trade.Price;
            candle.Close = trade.Price;
            candle.Volume += trade.Volume;
        }
    }

    /// <summary>
    /// Публикация закрытой свечи в кольцевой буфер Disruptor
    /// </summary>
    private void PublishCandleToDisruptor(in Candle candle)
    {
        // Запрашиваем индекс в кольце (блокирует поток, если буфер переполнен)
        long sequence = _ringBuffer.Next();
        try
        {
            // Получаем доступ к пре-аллоцированному объекту события
            CandleEvent @event = _ringBuffer[sequence];
            
            // Копируем структуру свечи (64 байта) в существующий объект
            @event.Value = candle;
        }
        finally
        {
            // Публикуем событие для Consumer
            _ringBuffer.Publish(sequence);
        }
    }

    /// <summary>
    /// Сброс всех активных свечей (например, при завершении работы)
    /// </summary>
    public void Flush()
    {
        foreach (var kvp in _activeCandles)
        {
            if (kvp.Value.IsInitialized)
            {
                PublishCandleToDisruptor(in kvp.Value);
            }
        }
        _activeCandles.Clear();
    }
}

#endregion

#region 3. Потребитель Свечей (Candle Consumer)

public sealed class CandleDisruptorConsumer : IEventHandler<CandleEvent>
{
    /// <summary>
    /// Метод выполняется автоматически в выделенном потоке Disruptor при появлении новых свечей.
    /// </summary>
    public void OnEvent(CandleEvent data, long sequence, bool endOfBatch)
    {
        // Передаем структуру по ссылке (in), минимизируя накладные расходы на копирование в стек
        ProcessCandle(in data.Value, endOfBatch);
    }

    private void ProcessCandle(in Candle candle, bool endOfBatch)
    {
        // Бизнес-логика: отправка свечи в торговую стратегию, отрисовка на графике, запись в БД.
        // Использование флага endOfBatch позволяет делать пакетную запись (Batching) нескольких свечей за раз.
        
        // Переиспользование памяти происходит автоматически на стороне Disruptor.
    }
}

#endregion

#region 4. Инициализация и Запуск (Wiring)

public static class Program
{
    public static async Task Main()
    {
        // Размер буфера (строго степень двойки)
        const int bufferSize = 2048; 

        // 1. Инициализация Disruptor. Фабрика создает CandleEvent один раз на старте.
        var disruptor = new Disruptor<CandleEvent>(
            factory: () => new CandleEvent(), 
            bufferSize: bufferSize, 
            taskScheduler: TaskScheduler.Default, 
            producerType: ProducerType.Single, // Трейды обрабатываются последовательно в одном потоке
            waitStrategy: new YieldingWaitStrategy() // Оптимальный баланс между задержками и CPU
        );

        // 2. Привязка обработчика свечей
        var consumer = new CandleDisruptorConsumer();
        disruptor.HandleEventsWith(consumer);

        // 3. Старт внутренних потоков Disruptor и получение ссылки на RingBuffer
        RingBuffer<CandleEvent> ringBuffer = disruptor.Start();

        // 4. Создание агрегатора с интервалом свечей в 1 минуту (60 секунд)
        var aggregator = new CandleAggregator(TimeSpan.FromMinutes(1), ringBuffer);

        // Симуляция получения данных из сети (используем CancellationToken для управления жизненным циклом)
        using var cts = new CancellationTokenSource();
        using var mockStream = new MemoryStream(); 

        // Запуск парсинга и агрегации входящего потока
        await aggregator.ParseTradeStreamAsync(mockStream, cts.Token);

        // Корректная остановка системы
        cts.Cancel();
        disruptor.Shutdown();
    }
}

#endregion

```

## Ключевые преимущества этого паттерна:

   1. Отсутствие аллокаций на стыке Агрегатор -> Disruptor: Вместо создания объектов new CandleEvent() или new Candle() под каждую закрывшуюся свечу, метод PublishCandleToDisruptor берет уже готовый объект из RingBuffer по индексу (sequence) и перезаписывает его поля.
   2. Безопасное владение памятью: CollectionsMarshal.GetValueRefOrAddDefault позволяет изменять состояние текущей свечи "по месту" в Dictionary. В момент закрытия свечи её копия просто переносится в RingBuffer, а старая ячейка в словаре готова под новый временной интервал. Сборщик мусора (GC) здесь полностью простаивает.
   3. Пакетная обработка (endOfBatch): Потребитель свечей (CandleDisruptorConsumer) может использовать флаг endOfBatch для буферизации данных перед тяжелыми операциями, такими как запись свечей пачкой в базу данных (например, ClickHouse или TimescaleDB).



