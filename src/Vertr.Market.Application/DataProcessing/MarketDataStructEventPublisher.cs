using Disruptor;
using Disruptor.Dsl;
using Vertr.Market.Application.Abstractions;

namespace Vertr.Market.Application.DataProcessing;

internal class MarketDataStructEventPublisher<T> : IMarketDataStructEventPublisher<T> where T : struct
{
    private readonly TimeSpan _publishInterval = TimeSpan.FromSeconds(1);
    private readonly Disruptor<MarketDataStructEvent<T>> _disruptor;

    // Глобальное хранилище, куда пишут рабочие потоки в реальном времени
    private readonly AtomicStructCell<T>[] _sharedStorage;

    public MarketDataStructEventPublisher(int arraySize)
    {
        _sharedStorage = new AtomicStructCell<T>[arraySize];
        for (var i = 0; i < arraySize; i++)
        {
            _sharedStorage[i] = new AtomicStructCell<T>();
        }

        _disruptor = new Disruptor<MarketDataStructEvent<T>>(() =>
        {
            var evt = new MarketDataStructEvent<T>();
            evt.Initialize(arraySize);
            return evt;
        }
        , ringBufferSize: 1024);

        // TODO: Refactor this
        _disruptor.HandleEventsWith(new SampleEventHandler<T>());
    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_publishInterval);
        _disruptor.Start();

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var sequence = _disruptor.RingBuffer.Next();
                try
                {
                    var targetEvent = _disruptor.RingBuffer[sequence];
                    targetEvent.Reset();
                    CopySnapshotWithValidation(targetEvent);
                }
                finally
                {
                    _disruptor.RingBuffer.Publish(sequence);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// ВЫЗЫВАЕТСЯ РАБОЧИМИ ПОТОКАМИ.
    /// Атомарная Lock-Free запись структуры T по указанному индексу.
    /// </summary>
    public void Update(int index, in T item)
    {
        var cell = _sharedStorage[index];
        Interlocked.Increment(ref cell.Version);
        cell.Data = item;
        Interlocked.Increment(ref cell.Version);
    }

    /// <summary>
    /// Копирует данные в массив. 
    /// Если в момент копирования поток писал в ячейку, операция повторится для этой ячейки.
    /// </summary>
    private void CopySnapshotWithValidation(MarketDataStructEvent<T> marketEvent)
    {
        for (var i = 0; i < _sharedStorage.Length; i++)
        {
            var srcCell = _sharedStorage[i];
            var destCell = marketEvent.Cells[i];

            while (true)
            {
                // Считываем версию до начала копирования
                var versionBefore = Volatile.Read(ref srcCell.Version);

                // Если версия нечетная — поток прямо сейчас пишет в структуру. Ждем микросекунду и пробуем снова.
                if (versionBefore % 2 != 0)
                {
                    Thread.SpinWait(1);
                    continue;
                }

                destCell.Data = srcCell.Data;

                var versionAfter = Volatile.Read(ref srcCell.Version);

                // Если версия не изменилась — копия валидна и атомарна. Переходим к следующему индексу.
                if (versionBefore == versionAfter)
                {
                    destCell.Version = versionAfter;

                    // ОЧИСТКА ХРАНИЛИЩА (если требуется сброс после отправки)
                    // Сбрасываем исходную ячейку в дефолтное состояние для следующего окна таймера
                    srcCell.Data = default;
                    Volatile.Write(ref srcCell.Version, 0);

                    break;
                }

                // Если версии не совпали, значит пишущий поток вклинился прямо во время копирования.
                // Цикл while повторит попытку чтения этой ячейки.
            }
        }
    }
}

internal class MarketDataStructEvent<T> where T : struct
{
    internal AtomicStructCell<T>[] Cells { get; private set; } = [];

    public void Initialize(int size)
    {
        Cells = new AtomicStructCell<T>[size];
        for (var i = 0; i < size; i++)
        {
            Cells[i] = new AtomicStructCell<T>();
        }
    }

    public void Reset()
    {
        for (var i = 0; i < Cells.Length; i++)
        {
            Cells[i].Version = 0;
            Cells[i].Data = default;
        }
    }
}

internal class SampleEventHandler<T> : IEventHandler<MarketDataStructEvent<T>> where T : struct
{
    public void OnEvent(MarketDataStructEvent<T> data, long sequence, bool endOfBatch)
    {
        Console.WriteLine(data);
    }
}

internal class AtomicStructCell<T> where T : struct
{
    // Счетчик версий: четный = данные стабильны, нечетный = идет запись
    public int Version;
    public T Data;
}
