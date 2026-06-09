using Disruptor;
using Disruptor.Dsl;
using Vertr.Market.Application.EventHandlers;
using Vertr.Market.Application.Models;

namespace Vertr.Market.ConsoleApp;

public static class Program
{
    public static async Task Main()
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
