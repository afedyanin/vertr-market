using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor;

namespace Vertr.Market.Application.Models;

public sealed class OrderBookThrottler
{
    private static readonly int OrderBookSize = Unsafe.SizeOf<OrderBook>();
    private readonly RingBuffer<OrderBookEvent> _ringBuffer;
    private readonly TimeSpan _interval;

    // Double-buffering: два буфера чередуются через атомарный swap.
    // _current — буфер, в который пишет парсер.
    // _snapshot — буфер, который станет _current на следующем тике (пустой, для накопления).
    private readonly OrderBookEvent _bufferA = new();
    private readonly OrderBookEvent _bufferB = new();
    private OrderBookEvent _current = null!;
    private OrderBookEvent _snapshot = null!;

    public OrderBookThrottler(TimeSpan interval, RingBuffer<OrderBookEvent> ringBuffer)
    {
        ArgumentNullException.ThrowIfNull(ringBuffer);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);

        _interval = interval;
        _ringBuffer = ringBuffer;
        _current = _bufferA;
        _snapshot = _bufferB;
    }

    public async ValueTask ParseStreamAsync(Stream stream, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new InvalidOperationException("Stream does not support reading.");
        }

        var buffer = new byte[OrderBookSize];
        var memoryBuffer = buffer.AsMemory();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await stream.ReadExactlyAsync(memoryBuffer, ct).ConfigureAwait(false);
                ref readonly var incomingBook = ref MemoryMarshal.AsRef<OrderBook>(buffer);

                if (incomingBook.AssetId < 0 || (uint)incomingBook.AssetId >= OrderBookEvent.Capacity)
                {
                    continue;
                }

                _current[incomingBook.AssetId] = incomingBook;
            }
        }
        catch (OperationCanceledException)
        {
            // Нормальное завершение работы
        }
        catch (IOException) when (ct.IsCancellationRequested)
        {
            // Сеть разорвана из-за отмены — не выбрасываем повторно
        }
        catch (ObjectDisposedException) when (ct.IsCancellationRequested)
        {
            // Stream был удалён из-за отмены — не выбрасываем повторно
        }
    }

    public async Task StartEmittingAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            try
            {
                // Атомарный swap: _snapshot становится текущим буфером для записи,
                // _current становится snapshot'ом для публикации.
                // Interlocked.Exchange гарантирует, что writer либо видит старый,
                // либо новый _current — не оба одновременно.
                var snapshot = Interlocked.Exchange(ref _current, _snapshot);
                _snapshot = snapshot;

                var sequence = _ringBuffer.Next();
                var targetEvent = _ringBuffer[sequence];
                targetEvent.CopyTo(snapshot);
                snapshot.Clear();

                _ringBuffer.Publish(sequence);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Disruptor может выбросить при переполнении RingBuffer или других ошибках.
                // При отмене — не логируем, так как это ожидаемое поведение.
            }
        }
    }
}
