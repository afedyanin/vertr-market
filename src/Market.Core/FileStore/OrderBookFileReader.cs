using System.Buffers;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using Market.Core.Models;
using MessagePack;

namespace Market.Core.FileStore;

public static class OrderBookFileReader
{
    public static async IAsyncEnumerable<OrderBookDepth> ReadAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new MessagePackStreamReader(gzipStream);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) is ReadOnlySequence<byte> sequence)
        {
            var batch = MessagePackSerializer.Deserialize<List<OrderBookDepth>>(sequence, cancellationToken: cancellationToken);

            foreach (var book in batch)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                yield return book;
            }
        }
    }
}
