using System.Buffers;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Market.Core.Models;

namespace Market.Core.FileStore;

public static class MarketDataFileAsyncBatchReader
{
    public static readonly int SizeOfMarketDepth = Marshal.SizeOf<OrderBookDepth>();

    public static IAsyncEnumerable<Memory<byte>> ReadMarketDepthBatchAsync(
        string filePath,
        int batchSize,
        CancellationToken cancellationToken = default)
        => ReadBatchAsync(filePath, batchSize, SizeOfMarketDepth, cancellationToken);

    private static async IAsyncEnumerable<Memory<byte>> ReadBatchAsync(
        string filePath,
        int batchSize,
        int itemSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Передаем токен в операции открытия файла
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);

        var maxBytesToRead = itemSize * batchSize;
        byte[]? byteBuffer = null;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Арендуем массив только под текущую итерацию
                byteBuffer = ArrayPool<byte>.Shared.Rent(maxBytesToRead);

                // Ограничиваем Memory строго запрашиваемым размером, а не размером массива из пула
                var byteMemory = byteBuffer.AsMemory(0, maxBytesToRead);

                // Читаем данные строго кратно itemSize
                var bytesRead = await ReadStrictBatchAsync(gzipStream, byteMemory, itemSize, cancellationToken).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                // Возвращаем Memory наружу. Данные валидны ТОЛЬКО до перехода к следующей итерации (yield)!
                yield return byteBuffer.AsMemory(0, bytesRead);

                // После того как вызывающий код обработал итерацию, возвращаем массив в пул
                ArrayPool<byte>.Shared.Return(byteBuffer);
                byteBuffer = null; // Обнуляем, чтобы не вернуть дважды в блоке finally
            }
        }
        finally
        {
            // Страховка на случай, если вызывающий код прервал foreach (break / exception)
            if (byteBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(byteBuffer);
            }
        }
    }

    private static async ValueTask<int> ReadStrictBatchAsync(Stream stream, Memory<byte> buffer, int itemSize, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.Slice(totalRead), ct).ConfigureAwait(false);
            if (read == 0)
            {
                // Защита от неполного элемента на конце файла
                var remainder = totalRead % itemSize;

                if (remainder != 0)
                {
                    throw new InvalidDataException($"Файл поврежден. Обнаружены незавершенные байты элемента ({remainder} байт).");
                }

                return totalRead;
            }

            totalRead += read;
        }

        return totalRead;
    }
}
