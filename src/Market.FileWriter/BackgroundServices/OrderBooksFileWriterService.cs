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
    private const string BaseDirectory = "books"; // Папка для логов

    private readonly ChannelReader<OrderBookDepth> _reader;
    private readonly ILogger<OrderBooksFileWriterService> _logger;

    // Состояние текущего файла
    private DateTime _currentFileDate = DateTime.MinValue;
    private FileStream? _fileStream;
    private GZipStream? _gzipStream;

    public OrderBooksFileWriterService(
        Channel<OrderBookDepth> channel,
        ILogger<OrderBooksFileWriterService> logger)
    {
        _reader = channel.Reader;
        _logger = logger;

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

                // Асинхронно ждем либо закрытия канала, либо следующего тика таймера
                if (!hasData)
                {
                    // Если канал пуст и закрыт (Writer.Complete()), WaitToReadAsync вернет false
                    if (!await _reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
                    {
                        // Канал закрылся, выходим из цикла для финального сброса
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Запись файлов остановлена по токену отмены.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка в процессе записи OrderBook в файлы.");
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
                    _logger.LogError(ex, "Не удалось сбросить финальный батч при закрытии.");
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

            _logger.LogInformation("Открытие нового файла для записи: {FilePath}", filePath);

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
                _logger.LogError(ex, "Ошибка при асинхронном закрытии GZipStream.");
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
                _logger.LogError(ex, "Ошибка при асинхронном закрытии FileStream.");
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
