using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using Market.ApiClient.Dtos;
using Market.ApiClient.Tcp.Dtos;
using MemoryPack;

namespace Market.ApiClient.Tcp.Internals;

internal interface ITcpApiClient : IDisposable
{
    event EventHandler? OnConnected;
    event EventHandler? OnDisconnected;

    Task ConnectAsync();
    Task<byte[]> SendRequestAsync<TRequest>(CommandType command, TRequest dto);
}

internal sealed class TcpApiClient : ITcpApiClient
{
    private readonly ITcpConnectionManager _connectionManager;
    private readonly IMessageProtocol _protocol;
    private readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<int, TaskCompletionSource<byte[]>> _pendingRequests = new();
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private readonly CancellationTokenSource _clientCts = new();

    private int _correlationIdCounter;
    private bool _isDisposed;

    public event EventHandler? OnConnected
    {
#pragma warning disable CA1030 // Use events where appropriate
        add => _connectionManager.OnConnected += value;
        remove => _connectionManager.OnConnected -= value;
    }

    public event EventHandler? OnDisconnected
    {
        add => _connectionManager.OnDisconnected += value;
        remove => _connectionManager.OnDisconnected -= value;
    }
#pragma warning restore CA1030 // Use events where appropriate

    public TcpApiClient(string host, int port)
        : this(new TcpConnectionManager(host, port), new MessageProtocol())
    {
    }

    public TcpApiClient(ITcpConnectionManager connectionManager, IMessageProtocol protocol)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
    }

    public async Task ConnectAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(ITcpApiClient));

        await _connectionManager.ConnectAsync(_clientCts.Token);
        _connectionManager.StartReading(ReadResponsesLoopAsync, _clientCts.Token);
    }

    public async Task<byte[]> SendRequestAsync<TRequest>(CommandType command, TRequest dto)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(ITcpApiClient));

        if (!_connectionManager.IsConnected)
        {
            throw new InvalidOperationException("Нет соединения с TCP сервером.");
        }

        int correlationId = Interlocked.Increment(ref _correlationIdCounter);
        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[correlationId] = tcs;

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
            byte[] packet = _protocol.Serialize(command, correlationId, dto);

            await _writeSemaphore.WaitAsync(timeoutCts.Token);
            try
            {
                Stream? stream = _connectionManager.Stream;

                if (stream == null)
                {
                    throw new InvalidOperationException("Соединение разорвано перед отправкой.");
                }

                await stream.WriteAsync(packet.AsMemory(), timeoutCts.Token);
                await stream.FlushAsync(timeoutCts.Token);
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

    private async Task ReadResponsesLoopAsync(PipeReader reader, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            ReadResult result = await reader.ReadAsync(token);
            ReadOnlySequence<byte> buffer = result.Buffer;

            while (_protocol.TryParsePacket(ref buffer, out RawPacket packet))
            {
                if (_pendingRequests.TryRemove(packet.CorrelationId, out var tcs))
                {
                    tcs.TrySetResult(packet.Payload);
                }
            }

            reader.AdvanceTo(buffer.Start, buffer.End);
            if (result.IsCompleted)
            {
                break;
            }
        }

        await reader.CompleteAsync();
    }

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
        _clientCts.Dispose();
        _writeSemaphore.Dispose();
        _connectionManager.Dispose();

        foreach (var kvp in _pendingRequests)
        {
#pragma warning disable MA0040 // Forward the CancellationToken parameter to methods that take one
            kvp.Value.TrySetCanceled();
#pragma warning restore MA0040 // Forward the CancellationToken parameter to methods that take one
        }

        _pendingRequests.Clear();
    }
}
