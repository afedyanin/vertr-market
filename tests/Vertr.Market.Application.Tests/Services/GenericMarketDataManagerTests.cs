using Disruptor;
using Vertr.Market.Application.Models;
using Vertr.Market.Application.Services;

namespace Vertr.Market.Application.Tests.Services;

public class GenericMarketDataManagerTests
{
    [Test]
    public void CanStartMarketDataManager()
    {
        var arraySize = 1000;

        // Инициализируем стандартный RingBuffer из библиотеки Disruptor
        var ringBuffer = RingBuffer<MarketDataEvent>.CreateMultiProducer(() => new MarketDataEvent(), 1024);

        // Создаем наш универсальный менеджер
        var manager = new GenericMarketDataManager<Bar, MarketDataEvent>(
            ringBuffer,
            arraySize,
            eventInitializer: (evt, size) =>
            {
                // Логика выделения массива внутри ивента (вызывается один раз при старте)
                evt.Bars = new Bar[size];
                for (var i = 0; i < size; i++)
                {
                    evt.Bars[i] = new Bar();
                }
            },
            arrayExtractor: (evt) => evt.Bars // Как менеджеру забрать массив из ивента
        );

        // Запуск фонового цикла публикации с шагом 10 миллисекунд
        //manager.StartPublishingLoop(TimeSpan.FromMilliseconds(10));

        // TODO: 
        //manager.UpdateAtomically();
    }
}
