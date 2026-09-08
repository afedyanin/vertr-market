using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;

namespace Market.ApiClient.Tcp.Internals;

internal sealed class TcpClientConnection : ITcpClientConnection
{
    private readonly ITcpConnectionManager _connectionManager;
    private readonly IMessageProtocol _protocol;
    private readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(TcpConsts.DefaulrRequestTimeoutSec);

    private readonly ConcurrentDictionary<int, TaskCompletionSource<byte[]>> _pendingRequests = new();
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);

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

    public TcpClientConnection(string host, int port)
        : this(new TcpConnectionManager(host, port), new MessageProtocol())
    {
    }

    public TcpClientConnection(ITcpConnectionManager connectionManager, IMessageProtocol protocol)
    {
        _connectionManager = connectionManager;
        _protocol = protocol;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(TcpClientConnection));

        await _connectionManager.ConnectAsync(cancellationToken);
        _connectionManager.StartReading(ReadResponsesLoopAsync, cancellationToken);
    }

    public async Task<byte[]> SendRequestAsync<TRequest>(CommandType command, TRequest dto)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(TcpClientConnection));

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
            await _writeSemaphore.WaitAsync(timeoutCts.Token);
            try
            {
                var packet = _protocol.Serialize(command, correlationId, dto);
                var stream = _connectionManager.Stream;

                if (stream == null)
                {
                    throw new InvalidOperationException("Соединение разорвано перед отправкой.");
                }

                await stream.WriteAsync(packet.AsMemory(), timeoutCts.Token);
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

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
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
