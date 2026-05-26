namespace Vertr.Market.Application.Abstractions;

public class AtomicCell<T> where T : class, IResetable<T>, new()
{
    private readonly T _bufferA = new();
    private readonly T _bufferB = new();
    private T _activeWriteBuffer;

    private int _isDirty;
    private int _writeLock;

    public AtomicCell()
    {
        _activeWriteBuffer = _bufferA;
    }

    public void UpdateInPlace(Action<T> updateAction)
    {
        while (Interlocked.CompareExchange(ref _writeLock, 1, 0) != 0)
        {
            Thread.SpinWait(1);
        }

        try
        {
            var writeTarget = _activeWriteBuffer;
            updateAction(writeTarget);
            Volatile.Write(ref _isDirty, 1);
        }
        finally
        {
            Interlocked.Exchange(ref _writeLock, 0);
        }
    }

    public bool TrySwapAndGetFilled(out T filledBuffer)
    {
        if (Interlocked.Exchange(ref _isDirty, 0) == 1)
        {
            var nextCleanBuffer = (_activeWriteBuffer == _bufferA) ? _bufferB : _bufferA;
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