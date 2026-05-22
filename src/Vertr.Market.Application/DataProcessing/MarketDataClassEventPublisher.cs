using Disruptor;
using Disruptor.Dsl;
using Vertr.Market.Application.Abstractions;

namespace Vertr.Market.Application.DataProcessing;

internal class MarketDataClassEventPublisher<T> where T : MarketDataItemBase, new()
{
    private readonly TimeSpan _publishInterval = TimeSpan.FromSeconds(1);
    private readonly Disruptor<MarketDataClassEvent<T>> _disruptor;

    // Глобальное хранилище, куда пишут рабочие потоки в реальном времени
    private readonly AtomicClassCell<T>[] _sharedStorage;

    public MarketDataClassEventPublisher(int arraySize)
    {
        _sharedStorage = new AtomicClassCell<T>[arraySize];
        for (var i = 0; i < arraySize; i++)
        {
            _sharedStorage[i] = new AtomicClassCell<T>();
        }

        _disruptor = new Disruptor<MarketDataClassEvent<T>>(() =>
        {
            var evt = new MarketDataClassEvent<T>();
            evt.Initialize(arraySize);
            return evt;
        }
        , ringBufferSize: 1024);

        // TODO: Refactor this
        _disruptor.HandleEventsWith(new SampleClassEventHandler<T>());
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
                    CopySnapshot(targetEvent);
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
        _sharedStorage[index].UpdateData(item);
    }

    /// <summary>
    /// Копирует данные в массив. 
    /// Если в момент копирования поток писал в ячейку, операция повторится для этой ячейки.
    /// </summary>
    private void CopySnapshot(MarketDataClassEvent<T> marketEvent)
    {
        for (var i = 0; i < _sharedStorage.Length; i++)
        {
            var cell = _sharedStorage[i];

            // 1. Забираем заполненный буфер у пишущего потока, подменяя его на чистый
            var filledBuffer = cell.SwapAndGetFilled();

            // 2. Копируем значения свойств в массив ивента Disruptor (Zero-allocation)
            marketEvent.Cells[i].CopyFrom(filledBuffer);

            // 3. ОЧИСТКА: Обнуляем свойства считанного буфера.
            // Теперь он полностью пустой и ждет, когда пишущий поток снова переключится на него.
            cell.ClearBuffer(filledBuffer);
        }
    }
}


internal class MarketDataClassEvent<T> where T : MarketDataItemBase, new()
{
    internal AtomicClassCell<T>[] Cells { get; private set; } = [];

    public void Initialize(int size)
    {
        Cells = new AtomicClassCell<T>[size];
        for (var i = 0; i < size; i++)
        {
            Cells[i] = new AtomicClassCell<T>();
        }
    }
}

internal class SampleClassEventHandler<T> : IEventHandler<MarketDataClassEvent<T>> where T : MarketDataItemBase, new()
{
    public void OnEvent(MarketDataClassEvent<T> data, long sequence, bool endOfBatch)
    {
        Console.WriteLine(data);
    }
}

internal class AtomicClassCell<T> where T : MarketDataItemBase, new()
{
    private readonly T _bufferA = new();
    private readonly T _bufferB = new();

    // Атомарный указатель на объект, в который сейчас РАЗРЕШЕНО писать рабочему потоку
    private T _activeWriteBuffer;

    public AtomicClassCell()
    {
        _activeWriteBuffer = _bufferA;
    }

    /// <summary>
    /// Вызывается пишущим потоком. Копирует свойства объекта in-place.
    /// </summary>
    public void UpdateData(T incomingData)
    {
        // Читаем ссылку на текущий буфер для записи
        var writeTarget = _activeWriteBuffer;

        // Копируем свойства ИЗ пришедшего объекта В наш внутренний буфер
        writeTarget.CopyFrom(incomingData);
    }

    /// <summary>
    /// Вызывается потоком таймера. 
    /// Атомарно забирает заполненный объект и подсовывает пишущему потоку чистый.
    /// </summary>
    public T SwapAndGetFilled()
    {
        // Определяем, какой буфер сейчас свободен и чист
        var nextCleanBuffer = (_activeWriteBuffer == _bufferA) ? _bufferB : _bufferA;

        // Атомарно меняем ссылку. Возвращаем тот буфер, в который поток ТОЛЬКО ЧТО писал.
        var filledBuffer = Interlocked.Exchange(ref _activeWriteBuffer, nextCleanBuffer);

        return filledBuffer;
    }

    public void ClearBuffer(T buffer)
    {
        buffer.Reset();
    }
}
