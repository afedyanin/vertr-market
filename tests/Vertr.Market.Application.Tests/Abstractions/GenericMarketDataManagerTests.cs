using Disruptor;
using Vertr.Market.Application.Abstractions;

namespace Vertr.Market.Application.Tests.Abstractions;

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
        manager.StartPublishingLoop(TimeSpan.FromMilliseconds(10));

        // TODO: 
        manager.UpdateAtomically();
    }
}


public class Bar : IResetable<Bar>
{
    public double Open { get; set; }
    public double Close { get; set; }
    public bool IsEmpty { get; set; } = true;

    public void CopyFrom(Bar source)
    {
        Open = source.Open;
        Close = source.Close;
        IsEmpty = false;
    }

    public void Reset()
    {
        Open = Close = 0;
        IsEmpty = true;
    }
}

public class MarketDataEvent
{
    public Bar[] Bars { get; set; } = [];
}