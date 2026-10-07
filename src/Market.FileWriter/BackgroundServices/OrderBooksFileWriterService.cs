using System.IO.Compression;
using System.Threading.Channels;
using Market.Core.Models;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Market.FileWriter.BackgroundServices;

public class OrderBooksFileWriterService : BackgroundService
{
    private const int BatchSize = 1000;
    private static readonly TimeSpan BatchTimeout = TimeSpan.FromSeconds(5);
    private const string BaseDirectory = "books";

    private readonly ChannelReader<OrderBookDepth> _reader;
    private readonly ILogger<OrderBooksFileWriterService> _logger;

    // Состояние текущего файла
    private DateTime _currentFileDate = DateTime.MinValue;
    private FileStream? _fileStream;
    private GZipStream? _gzipStream;

    public OrderBooksFileWriterService(
        Channel<OrderBookDepth> channel,
        ILoggerFactory loggerFacrtory)
    {
        _reader = channel.Reader;
        _logger = loggerFacrtory.CreateLogger<OrderBooksFileWriterService>();

        // Создаем директорию, если её нет
        if (!Directory.Exists(BaseDirectory))
        {
            Directory.CreateDirectory(BaseDirectory);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting order book file writer service.");

        var batch = new List<OrderBookDepth>(BatchSize);
        var lastFlush = DateTime.UtcNow;

        try
        {
            // Используем периодический таймер для контроля таймаутов сброса и роллинга
            using var timer = new PeriodicTimer(BatchTimeout);

            // Запускаем два параллельных ожидания: появление данных или тики таймера
            while (!stoppingToken.IsCancellationRequested)
            {
                bool hasData = false;

                // Читаем всё, что накопилось в канале прямо сейчас (без блокировок)
                while (_reader.TryRead(out var book))
                {
                    hasData = true;

                    // Проверяем, не наступил ли новый день перед добавлением в батч
                    await EnsureCorrectFileAsync(DateTime.UtcNow).ConfigureAwait(false);

                    batch.Add(book);

                    if (batch.Count >= BatchSize)
                    {
                        await FlushBatchAsync(batch, stoppingToken).ConfigureAwait(false);
                        lastFlush = DateTime.UtcNow;
                    }
                }

                // Если данные были записаны или сработал тайм-аут по времени
                if (batch.Count > 0 && (DateTime.UtcNow - lastFlush) >= BatchTimeout)
                {
                    await FlushBatchAsync(batch, stoppingToken).ConfigureAwait(false);
                    lastFlush = DateTime.UtcNow;
                }

                // Проверяем смену даты по таймеру, даже если канал пуст (чтобы закрыть старый файл)
                await EnsureCorrectFileAsync(DateTime.UtcNow).ConfigureAwait(false);

                // Асинхронно ждем либо появления данных, либо следующего тика таймера
                if (!hasData)
                {
                    using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

                    var dataTask = _reader.WaitToReadAsync(waitCts.Token).AsTask();
                    var tickTask = timer.WaitForNextTickAsync(waitCts.Token).AsTask();

                    var completed = await Task.WhenAny(dataTask, tickTask).ConfigureAwait(false);

                    // Отменяем проигравшее ожидание, чтобы не оставлять висящие задачи
                    await waitCts.CancelAsync().ConfigureAwait(false);

                    if (completed == dataTask)
                    {
                        // Если канал пуст и закрыт (Writer.Complete()), WaitToReadAsync вернет false
                        if (!await dataTask.ConfigureAwait(false))
                        {
                            // Канал закрылся, выходим из цикла для финального сброса
                            break;
                        }
                    }
                    else
                    {
                        // Тик таймера: цикл выполнит таймаутный флаш и смену файла
                        await tickTask.ConfigureAwait(false);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Order book file writer service cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OrderBook file writing error. Message={Message}", ex.Message);
        }
        finally
        {
            // Финальный сброс остатков и закрытие всех потоков
            if (batch.Count > 0)
            {
                try
                {
                    await FlushBatchAsync(batch, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Flash batch error. Message={Message}", ex.Message);
                }
            }

            await CloseCurrentFileAsync().ConfigureAwait(false);
        }

        _logger.LogInformation("Order book file writer service stopped.");
    }

    private async Task EnsureCorrectFileAsync(DateTime now)
    {
        var today = now.Date;

        // Если дата сменилась или это самый первый запуск
        if (_fileStream == null || today != _currentFileDate)
        {
            // Закрываем предыдущий день, если он был открыт
            await CloseCurrentFileAsync().ConfigureAwait(false);

            _currentFileDate = today;
            // Формируем имя файла вида: logs/books_2026-10-05.bin.gz
            string filePath = Path.Combine(BaseDirectory, $"books_{today:yyyy-MM-dd}.bin.gz");

            _logger.LogInformation("Open file to write: {FilePath}", filePath);

            // Открываем потоки для нового дня
            _fileStream = new FileStream(
                filePath,
                FileMode.Append, // Используем Append, на случай если сервис перезапускался в течение дня
                FileAccess.Write,
                FileShare.Read,  // Разрешаем чтение файла другими процессами во время записи
                bufferSize: 65536,
                useAsync: true);

            _gzipStream = new GZipStream(_fileStream, CompressionLevel.Fastest);
        }
    }

    /// <summary>
    /// Асинхронное закрытие и очистка потоков сброса на диск.
    /// </summary>
    private async Task CloseCurrentFileAsync()
    {
        // Сначала корректно закрываем GZipStream, чтобы он записал свои завершающие байты (футер архива)
        if (_gzipStream != null)
        {
            try
            {
                await _gzipStream.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GZipStream error. Message={Message}", ex.Message);
            }
            finally
            {
                _gzipStream = null;
            }
        }

        // Затем закрываем базовый файловый поток
        if (_fileStream != null)
        {
            try
            {
                await _fileStream.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FileStream error. Message={Message}", ex.Message);
            }
            finally
            {
                _fileStream = null;
            }
        }
    }

    private async Task FlushBatchAsync(List<OrderBookDepth> batch, CancellationToken cancellationToken)
    {
        if (_gzipStream == null)
        {
            return;
        }

        await MessagePackSerializer.SerializeAsync(_gzipStream, batch, cancellationToken: cancellationToken).ConfigureAwait(false);
        await _gzipStream.FlushAsync(cancellationToken).ConfigureAwait(false);

        batch.Clear();
    }

    public override void Dispose()
    {
        // Если потоки еще живы (например, приложение завершилось аварийно) — уничтожаем их синхронно
        _gzipStream?.Dispose();
        _fileStream?.Dispose();

        base.Dispose();
    }
}
