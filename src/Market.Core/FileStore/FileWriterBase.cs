using System.Collections.Concurrent;
using System.IO.Compression;

namespace Market.Core.FileStore;

public abstract class FileWriterBase : IAsyncDisposable
{
    private readonly ConcurrentDictionary<StreamCacheKey, CompressedStreamContext> _activeStreams = new();

    private readonly string _outputDirectory;
    private readonly Timer _flushTimer;
    private readonly string _fileNameSuufix;

#pragma warning disable CA1805 // Do not initialize unnecessarily
    private int _isDisposed = 0;
    private int _isFlushing = 0; // Флаг атомарной блокировки для таймера
#pragma warning restore CA1805 // Do not initialize unnecessarily

    protected FileWriterBase(string outputDirectory, TimeSpan flushInterval, string fileNameSuffix)
    {
        _fileNameSuufix = fileNameSuffix;
        _outputDirectory = outputDirectory;

        if (!Directory.Exists(_outputDirectory))
        {
            Directory.CreateDirectory(_outputDirectory);
        }

        // Запускаем фоновый таймер, который будет периодически сбрасывать буферы на диск
        _flushTimer = new Timer(OnFlushTimer, null, flushInterval, flushInterval);
    }

    internal CompressedStreamContext GetOrCreateContext(StreamCacheKey streamKey)
    {
        // Передаем state (кортеж параметров) третьим аргументом, чтобы избежать аллокации замыкания lambda-функции
        return _activeStreams.GetOrAdd(streamKey, static (key, state) =>
        {
            // Этот код выполнится ровно ОДИН раз для новой пары Asset+Дата
            var fileName = key.ToFileName(state.Suffix);
            var filePath = Path.Combine(state.Dir, fileName);

            var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read, 65536, useAsync: false);
            var gzipStream = new GZipStream(fileStream, CompressionLevel.Fastest, leaveOpen: true);

            return new CompressedStreamContext(fileStream, gzipStream);
        }, (Dir: _outputDirectory, Suffix: _fileNameSuufix));
    }

    /// <summary>
    /// Фоновый периодический сброс накопленных буферов и очистка устаревших стримов
    /// </summary>
    private void OnFlushTimer(object? state)
    {
        if (Interlocked.CompareExchange(ref _isFlushing, 1, 0) != 0)
        {
            return;
        }

        try
        {
            // 1. Получаем текущую дату в виде компактного числа int (0 аллокаций)
            var now = DateTime.UtcNow;
            var currentDateKey = (now.Year * 10000) + (now.Month * 100) + now.Day;

            // Используем прямой перебор пар. Это эффективнее, так как .Keys копирует ключи в новый массив/список
            foreach (var kvp in _activeStreams)
            {
                var key = kvp.Key;
                var context = kvp.Value;

                // 2. Если файл относится к текущему дню — просто делаем Flush
                if (key.DateKey == currentDateKey)
                {
                    if (Monitor.TryEnter(context.LockObject))
                    {
                        try
                        {
                            context.GzipStream.Flush();
                            context.FileStream.Flush(flushToDisk: false);
                        }
                        finally
                        {
                            Monitor.Exit(context.LockObject);
                        }
                    }
                }
                // 3. Если день сменился (или данные из прошлого) — плавно закрываем старый файл
                else
                {
                    // Удаляем по структурному ключу (без аллокаций строк)
                    if (_activeStreams.TryRemove(key, out var oldContext))
                    {
                        lock (oldContext.LockObject)
                        {
                            try
                            {
                                // Закрываем вчерашний файл, дописывая футеры GZip и очищая буферы
                                oldContext.DisposeAsync().AsTask().GetAwaiter().GetResult();
                            }
                            catch (Exception ex)
                            {
                                // TODO: Заменить на ILogger
                                System.Diagnostics.Trace.WriteLine($"Ошибка при закрытии файла стрима (AssetId: {key.AssetId}): {ex.Message}");
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isFlushing, 0);
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Гарантируем, что Dispose выполнится строго один раз
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        // 1. Останавливаем и уничтожаем таймер, чтобы новые итерации Flush не запускались
        if (_flushTimer != null)
        {
            await _flushTimer.DisposeAsync().ConfigureAwait(false);
        }

        // 2. Ожидаем завершения активного Flush (если он идет прямо сейчас)
        // Крутимся в легковесном SpinWait, пока флаг _isFlushing не станет равен 0
        var spinWait = new SpinWait();
        while (Volatile.Read(ref _isFlushing) == 1)
        {
            spinWait.SpinOnce();
        }

        // 3. Извлекаем все оставшиеся контексты из словаря, атомарно очищая его
        var contextsToDispose = new List<CompressedStreamContext>(_activeStreams.Count);
        foreach (var key in _activeStreams.Keys)
        {
            if (_activeStreams.TryRemove(key, out var context))
            {
                contextsToDispose.Add(context);
            }
        }

        // 4. Параллельно запускаем закрытие всех файлов
        // Обертка в Task.Run позволяет разгрузить вызывающий поток, если файлов очень много
        if (contextsToDispose.Count > 0)
        {
            try
            {
                // Запускаем асинхронные задачи закрытия параллельно через Task.WhenAll
                var disposeTasks = contextsToDispose.Select(async context =>
                {
                    // Захватываем лок на контекст, чтобы избежать гонки, если кто-то еще пытался писать
                    lock (context.LockObject)
                    {
                        // Внутри DisposeAsync стрим допишет GZip-футер и сбросит буферы на диск
                        // Вызываем честный асинхронный метод, так как мы находимся в async-контексте
                        return context.DisposeAsync().AsTask();
                    }
                });

                await Task.WhenAll(disposeTasks).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // TODO: Заменить на ILogger
                System.Diagnostics.Trace.WriteLine($"Ошибка при массовом закрытии файлов в DisposeAsync: {ex.Message}");
            }
        }

        _activeStreams.Clear();

        GC.SuppressFinalize(this);
    }

    internal sealed class CompressedStreamContext : IAsyncDisposable
    {
        public FileStream FileStream { get; }
        public GZipStream GzipStream { get; }
        public object LockObject { get; } = new();

        public CompressedStreamContext(FileStream fileStream, GZipStream gzipStream)
        {
            FileStream = fileStream;
            GzipStream = gzipStream;
        }

        public async ValueTask DisposeAsync()
        {
            if (GzipStream != null)
            {
                await GzipStream.DisposeAsync();
            }

            if (FileStream != null)
            {
                await FileStream.DisposeAsync();
            }
        }
    }
}

public readonly record struct StreamCacheKey(ushort AssetId, int DateKey)
{
    // Метод генерации имени файла, который сработает ТОЛЬКО при создании нового потока
    public string ToFileName(string suffix)
    {
        // Распаковываем дату обратно
        var year = DateKey / 10000;
        var month = (DateKey % 10000) / 100;
        var day = DateKey % 100;

        // Используем string.Create для сборки имени за 1 аллокацию
        var idLength = AssetId < 10 ? 1 : AssetId < 100 ? 2 : AssetId < 1000 ? 3 : AssetId < 10000 ? 4 : 5;
        var totalLength = idLength + 1 + 10 + suffix.Length; // id + '_' + yyyy-MM-dd + suffix

        return string.Create(totalLength, (AssetId, year, month, day, suffix), (span, state) =>
        {
            state.AssetId.TryFormat(span, out var written);
            span[written++] = '_';

            // Форматируем дату вручную yyyy-MM-dd
            state.year.TryFormat(span.Slice(written), out _, "D4");
            span[written + 4] = '-';
            state.month.TryFormat(span.Slice(written + 5), out _, "D2");
            span[written + 7] = '-';
            state.day.TryFormat(span.Slice(written + 8), out _, "D2");
            written += 10;

            state.suffix.AsSpan().CopyTo(span.Slice(written));
        });
    }
}
