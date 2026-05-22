namespace Vertr.Market.Application.Abstractions;

public class AtomicCell<T> where T : class, IResetable<T>, new()
{
    private readonly T _bufferA = new();
    private readonly T _bufferB = new();
    private T _activeWriteBuffer;

    // Флаг изменений: 0 = изменений нет, 1 = данные обновились
    private int _isDirty;

    // Спин-лок для предотвращения race condition МЕЖДУ пишущими потоками ПО ОДНОМУ ИНДЕКСУ
    private int _writeLock;

    public AtomicCell()
    {
        _activeWriteBuffer = _bufferA;
    }


    /// <summary>
    /// Позволяет потоку безопасно агрегировать данные прямо внутри текущего рабочего буфера
    /// </summary>
    public void UpdateInPlace(Action<T> updateAction)
    {
        // Оптимистичная ультра-быстрая блокировка только для авторов сделок ПО ОДНОМУ ИНДЕКСУ.
        // Если сделки по инструменту "Биткоин" летят параллельно, они выстроятся в микро-очередь на наносекунды.
        // Инструмент "Эфириум" при этом будет работать абсолютно независимо на другом ядре CPU.
        while (Interlocked.CompareExchange(ref _writeLock, 1, 0) != 0)
        {
            Thread.SpinWait(1);
        }

        try
        {
            var writeTarget = _activeWriteBuffer;

            // Выполняем логику расчета (например, OHLVC агрегацию)
            updateAction(writeTarget);

            // Помечаем ячейку грязной для таймера
            Volatile.Write(ref _isDirty, 1);
        }
        finally
        {
            // Освобождаем микро-блок
            Interlocked.Exchange(ref _writeLock, 0);
        }
    }

    /*
    /// <summary>
    /// Локальная Lock-Free запись данных в текущий активный буфер.
    /// </summary>
    public void UpdateData(T incomingData)
    {
        var writeTarget = _activeWriteBuffer;
        writeTarget.CopyFrom(incomingData);

        // Барьер памяти: гарантируем, что флаг взведется строго ПОСЛЕ копирования данных
        Volatile.Write(ref _isDirty, 1);
    }
    */

    /// <summary>
    /// Проверяет флаг изменений и атомарно переключает буферы.
    /// </summary>
    public bool TrySwapAndGetFilled(out T filledBuffer)
    {
        // Атомарно считываем флаг и сбрасываем его в 0 за 1 такт процессора
        if (Interlocked.Exchange(ref _isDirty, 0) == 1)
        {
            var nextCleanBuffer = (_activeWriteBuffer == _bufferA) ? _bufferB : _bufferA;

            // Подменяем пишущему потоку буфер на чистый, возвращаем заполненный
            filledBuffer = Interlocked.Exchange(ref _activeWriteBuffer, nextCleanBuffer);
            return true;
        }

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        filledBuffer = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        return false;
    }

    public void ClearBuffer(T buffer)
    {
        buffer.Reset();
    }
}