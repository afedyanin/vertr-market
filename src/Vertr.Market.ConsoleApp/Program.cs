using Disruptor;
using Disruptor.Dsl;
using Vertr.Market.Application.EventHandlers;
using Vertr.Market.Application.Models;

namespace Vertr.Market.ConsoleApp;

public static class Program
{
    public static async Task Main()
    {
    }

    public static async Task StartTradesProcessing()
    {
        // Размер буфера (строго степень двойки)
        const int bufferSize = 2048;

        // 1. Инициализация Disruptor. Фабрика создает CandleEvent один раз на старте.
        var disruptor = new Disruptor<CandleEvent>(
            eventFactory: () => new CandleEvent(),
            ringBufferSize: bufferSize,
            taskScheduler: TaskScheduler.Default,
            producerType: ProducerType.Single, // Трейды обрабатываются последовательно в одном потоке
            waitStrategy: new YieldingWaitStrategy() // Оптимальный баланс между задержками и CPU
        );

        // 2. Привязка обработчика свечей
        var consumer = new CandleDisruptorConsumer();
        disruptor.HandleEventsWith(consumer);

        // 3. Старт внутренних потоков Disruptor и получение ссылки на RingBuffer
        var ringBuffer = disruptor.Start();

        // 4. Создание агрегатора с интервалом свечей в 1 минуту (60 секунд)
        var aggregator = new CandleAggregator(TimeSpan.FromMinutes(1), ringBuffer);

        // Симуляция получения данных из сети (используем CancellationToken для управления жизненным циклом)
        using var cts = new CancellationTokenSource();
        using var mockStream = new MemoryStream();

        // Запуск парсинга и агрегации входящего потока
        await aggregator.ParseTradeStreamAsync(mockStream, cts.Token);

        // Корректная остановка системы
        await cts.CancelAsync();
        disruptor.Shutdown();
    }

    public static async Task StartOrderBooksProcessing()
    {
        // Размер буфера ДОЛЖЕН быть строго степенью двойки
        const int bufferSize = 1024;
        // 1. Инициализируем Disruptor. Передаем фабрику для пре-аллокации наших классов-событий.
        var disruptor = new Disruptor<OrderBookEvent>(
            eventFactory: () => new OrderBookEvent(),
            ringBufferSize: bufferSize,
            taskScheduler: TaskScheduler.Default,
            producerType: ProducerType.Single, // У нас ровно один поток-издатель (таймер троттлера)
            waitStrategy: new YieldingWaitStrategy() // Оптимальный компромисс между задержкой и CPU
        );

        // 2. Регистрируем наш обработчик (Consumer)
        var consumer = new OrderBookDisruptorConsumer();
        disruptor.HandleEventsWith(consumer);

        // 3. Запускаем внутренние потоки Disruptor и получаем доступ к RingBuffer
        var ringBuffer = disruptor.Start();

        // 4. Создаем Троттлер и передаем ему ссылку на RingBuffer
        var throttler = new OrderBookThrottler(TimeSpan.FromSeconds(5), ringBuffer);

        // Запуск фонового таймера отправки срезов (раз в 5 секунд)
        using var cts = new CancellationTokenSource();
        var emittingTask = throttler.StartEmittingAsync(cts.Token);

        // Симуляция: Передаем пустой стрим для демонстрации парсинга (в реальности здесь будет NetworkStream/PipeReader)
        using var mockStream = new MemoryStream();
        await throttler.ParseStreamAsync(mockStream, cts.Token);

        // Корректное завершение при остановке приложения
        await cts.CancelAsync();
        await emittingTask;
        disruptor.Shutdown();
    }
}
