using System.IO.Compression;
using System.Threading.Channels;
using Market.Core.Models;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Market.FileWriter.BackgroundServices;

public class OrderBookFileWriterService : BackgroundService
{
    private const int BatchSize = 1000; // Сбрасываем на диск каждые 1000 объектов
    private static readonly TimeSpan BatchTimeout = TimeSpan.FromSeconds(5); // Или каждые 5 секунд
    private const string FilePath = "books.bin.gz";

    private readonly ChannelReader<OrderBookDepth> _reader;
    private readonly ILogger<OrderBookFileWriterService> _logger;

    public OrderBookFileWriterService(
        Channel<OrderBookDepth> channel,
        ILogger<OrderBookFileWriterService> logger)
    {
        _reader = channel.Reader;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var fileStream = new FileStream(
            FilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 65536,
            useAsync: true);

        await using var gzipStream = new GZipStream(fileStream, CompressionLevel.Fastest);

        var batch = new List<OrderBookDepth>(BatchSize);
        var lastFlush = DateTime.UtcNow;

        try
        {
            // Читаем из канала, пока он не будет закрыт (Writer.Complete())
            while (await _reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            {
                while (_reader.TryRead(out var book))
                {
                    batch.Add(book);

                    if (batch.Count >= BatchSize || (DateTime.UtcNow - lastFlush) >= BatchTimeout)
                    {
                        await FlushBatchAsync(gzipStream, batch, stoppingToken);
                        lastFlush = DateTime.UtcNow;
                    }
                }

                // Дополнительная проверка по тайм-ауту, если новые элементы временно перестали поступать
                if (batch.Count > 0 && (DateTime.UtcNow - lastFlush) >= BatchTimeout)
                {
                    await FlushBatchAsync(gzipStream, batch, stoppingToken);
                    lastFlush = DateTime.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Нормальное завершение по токену
        }
        finally
        {
            // Сбрасываем остатки перед выходом
            if (batch.Count > 0)
            {
                await FlushBatchAsync(gzipStream, batch, stoppingToken);
            }
        }
    }

    private static async Task FlushBatchAsync(GZipStream stream, List<OrderBookDepth> batch, CancellationToken cancellationToken)
    {
        await MessagePackSerializer.SerializeAsync(stream, batch, cancellationToken: cancellationToken).ConfigureAwait(false);
        batch.Clear();
    }
}
