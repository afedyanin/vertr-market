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
        // 1. Создаем RingBuffer (размер строго степень двойки)
        var disruptor = new Disruptor<OrderBookEvent>(
            () => new OrderBookEvent(),
            ringBufferSize: 4096,
            TaskScheduler.Default,
            ProducerType.Single, // У нас один издатель (PipeReader), ставим Single для Low-Latency
            new YieldingWaitStrategy() // Агрессивная стратегия ожидания для минимального latency
        );

        // 2. Создаем пул из 4-х параллельных воркеров
        var cpuCount = 4;
        var processors = new OrderBookProcessor[cpuCount];
        for (var i = 0; i < cpuCount; i++)
        {
            processors[i] = new OrderBookProcessor(
                processorId: i,
                totalProcessors: cpuCount
            );
        }

        // 3. Регистрируем их в Disruptor параллельно 
        // (Они будут читать из RingBuffer одновременно, каждый забирая свои AssetId)
        disruptor.HandleEventsWith(processors);

        // 4. Запуск инфраструктуры потребителей
        var ringBuffer = disruptor.Start();

        // 5. Передаем ringBuffer в наш OrderBookPublisher (код из прошлых шагов)
        var publisher = new OrderBookPublisher(ringBuffer);
        // Симуляция: Передаем пустой стрим для демонстрации парсинга (в реальности здесь будет NetworkStream/PipeReader)
        using var cts = new CancellationTokenSource();
        using var mockStream = new MemoryStream();
        await publisher.ParseStreamAsync(mockStream, cts.Token);

        // Корректное завершение при остановке приложения
        await cts.CancelAsync();
        disruptor.Shutdown();
    }
}
