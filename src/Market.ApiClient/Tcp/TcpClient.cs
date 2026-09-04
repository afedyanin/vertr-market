using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net.Sockets;
using Market.ApiClient.Dtos;
using Market.ApiClient.Tcp.Dtos;
using MemoryPack;

namespace Market.ApiClient.Tcp;

public sealed class TcpClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(2);

    private System.Net.Sockets.TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private PipeReader? _pipeReader;

#pragma warning disable CA1805 // Do not initialize unnecessarily
    private int _correlationIdCounter = 0;
#pragma warning restore CA1805 // Do not initialize unnecessarily
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);

    private readonly CancellationTokenSource _clientCts = new();
    private bool _isDisposed;

    // Словарь ожидания ответов
    private readonly ConcurrentDictionary<int, TaskCompletionSource<byte[]>> _pendingRequests = new();

#pragma warning disable CA1003 // Use generic event handler instances
    public event Action? OnConnected;
    public event Action? OnDisconnected;
#pragma warning restore CA1003 // Use generic event handler instances

    public TcpClient(string host, int port)
    {
        _host = host;
        _port = port;
    }

    /// <summary>
    /// Первичное подключение клиента
    /// </summary>
    public async Task ConnectAsync()
    {
        await ConnectInternalAsync();
    }

    private async Task ConnectInternalAsync()
    {
        await _connectionSemaphore.WaitAsync(_clientCts.Token);
        try
        {
            if (_tcpClient?.Connected == true)
            {
                return;
            }

            CleanUpCurrentConnection();

            this._tcpClient = new System.Net.Sockets.TcpClient();
            await _tcpClient.ConnectAsync(_host, _port, _clientCts.Token);
            _stream = _tcpClient.GetStream();
            _pipeReader = PipeReader.Create(_stream);

            _ = ReadResponsesLoopAsync(_pipeReader, _clientCts.Token);

            OnConnected?.Invoke();
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Логика автоматического переподключения
    /// </summary>
    private async Task StartReconnectLoopAsync()
    {
        OnDisconnected?.Invoke();
        CleanUpCurrentConnection();

        while (!_clientCts.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_reconnectDelay, _clientCts.Token);
                await ConnectInternalAsync();
                break; // Успешно подключились — выходим из цикла реконнекта
            }
            catch
            {
                // Игнорируем ошибки неудачных попыток и пробуем снова через delay
            }
        }
    }

    /// <summary>
    /// Отправка запроса с поддержкой таймаута и контроля переподключения
    /// </summary>
    private async Task<byte[]> SendRequestAsync<TRequest>(CommandType command, TRequest dto)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(TcpClient));

        // Если связи нет, пробуем инициировать подключение (или падаем, зависит от бизнес-логики)
        if (_stream == null || !_tcpClient!.Connected)
        {
            throw new InvalidOperationException("Нет соединения с TCP сервером.");
        }

        int correlationId = Interlocked.Increment(ref _correlationIdCounter);
        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[correlationId] = tcs;

        // Настройка таймаута запроса
        using var timeoutCts = new CancellationTokenSource(_requestTimeout);
        using var registration = timeoutCts.Token.Register(() =>
        {
            if (_pendingRequests.TryRemove(correlationId, out var pendingTcs))
            {
                pendingTcs.TrySetException(new TimeoutException($"Запрос {command} (ID: {correlationId}) превысил таймаут {_requestTimeout.TotalSeconds} сек."));
            }
        });

        try
        {
            byte[] payload = MemoryPackSerializer.Serialize(dto);
            int totalLength = 4 + 2 + 4 + payload.Length;

            byte[] packet = new byte[totalLength];
            using (var ms = new MemoryStream(packet))
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(System.Net.IPAddress.HostToNetworkOrder(totalLength));
                writer.Write(System.Net.IPAddress.HostToNetworkOrder((short)command));
                writer.Write(System.Net.IPAddress.HostToNetworkOrder(correlationId));
                writer.Write(payload);
            }

            await _writeSemaphore.WaitAsync(timeoutCts.Token);
            try
            {
                // Перепроверяем поток внутри критической секции
                if (_stream == null)
                {
                    throw new InvalidOperationException("Соединение разорвано перед отправкой.");
                }

                await _stream.WriteAsync(packet.AsMemory(0, packet.Length), timeoutCts.Token);
                await _stream.FlushAsync(timeoutCts.Token);
            }
            finally
            {
                _writeSemaphore.Release();
            }

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            _pendingRequests.TryRemove(correlationId, out _);
            if (ex is OperationCanceledException && timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException($"Запрос {command} был прерван по таймауту.");
            }

            throw;
        }
    }

    /// <summary>
    /// Фоновый цикл разбора пакетов
    /// </summary>
    private async Task ReadResponsesLoopAsync(PipeReader reader, CancellationToken token)
    {
        const int headerSize = 10;
        bool isFaulted = false;

        try
        {
            while (!token.IsCancellationRequested)
            {
                ReadResult result = await reader.ReadAsync(token);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (true)
                {
                    if (buffer.Length < headerSize)
                    {
                        break;
                    }

                    SequenceReader<byte> seqReader = new SequenceReader<byte>(buffer);
                    seqReader.TryReadBigEndian(out int packetLength);

                    if (buffer.Length < packetLength)
                    {
                        break;
                    }

                    seqReader.TryReadBigEndian(out short commandId);
                    seqReader.TryReadBigEndian(out int correlationId);

                    int payloadLength = packetLength - headerSize;
                    byte[] payload = new byte[payloadLength];
                    buffer.Slice(seqReader.Position, payloadLength).CopyTo(payload);

                    buffer = buffer.Slice(buffer.GetPosition(packetLength));

                    if (_pendingRequests.TryRemove(correlationId, out var tcs))
                    {
                        tcs.TrySetResult(payload);
                    }
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted)
                {
                    break;
                }// Сервер закрыл соединение
            }
        }
        catch
        {
            isFaulted = true;
        }
        finally
        {
            await reader.CompleteAsync();

            // Если чтение прервалось из-за сбоя сокета или закрытия сервером — запускаем реконнект
            if ((isFaulted || !token.IsCancellationRequested) && !_isDisposed)
            {
                _ = Task.Run(StartReconnectLoopAsync, _clientCts.Token);
            }
        }
    }

    private void CleanUpCurrentConnection()
    {
        _stream?.Dispose();
        _tcpClient?.Dispose();

        _stream = null;
        _tcpClient = null;
        _pipeReader = null;

        // Сбрасываем все текущие запросы, которые ждали ответа на сломанном сокете
        var exception = new SocketException((int)SocketError.ConnectionReset);
        foreach (var tcs in _pendingRequests.Values)
        {
            tcs.TrySetException(exception);
        }

        _pendingRequests.Clear();
    }

    // --- МЕТОДЫ КОНТРАКТА ---

    public async Task<MarketDepthDto[]> GetBooks(int assetId, int count = 1)
    {
        var requestDto = new GetBooksRequestDto { AssetId = (ushort)assetId, Count = count };
        byte[] responseBytes = await SendRequestAsync(CommandType.GetBooksRequest, requestDto);
        return MemoryPackSerializer.Deserialize<MarketDepthDto[]>(responseBytes) ?? [];
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        _clientCts.Cancel();
        CleanUpCurrentConnection();

        _clientCts.Dispose();
        _writeSemaphore.Dispose();
        _connectionSemaphore.Dispose();
    }
}
