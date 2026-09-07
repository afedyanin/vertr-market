using System.IO.Pipelines;
using System.Net.Sockets;

namespace Market.ApiClient.Tcp.Internals;

internal interface ITcpConnectionManager : IDisposable
{
    event EventHandler? OnConnected;
    event EventHandler? OnDisconnected;

    bool IsConnected { get; }
    Stream? Stream { get; }

    Task ConnectAsync(CancellationToken cancellationToken);
    void StartReading(Func<PipeReader, CancellationToken, Task> readLoopFactory, CancellationToken cancellationToken);
    void ForceReconnect();
}

internal sealed class TcpConnectionManager : ITcpConnectionManager
{
    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(2);
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    private readonly CancellationTokenSource _managerCts = new();
    private TcpClient? _tcpClient;
    private Stream? _stream;
    private CancellationTokenSource? _loopCts;
    private bool _isDisposed;
    private Func<PipeReader, CancellationToken, Task>? _readLoopFactory;

    // 0 = no read loop running, 1 = a read loop is running. Guards against spawning more than
    // one PipeReader over the same NetworkStream (see StartReadLoop).
    private int _readLoopActive;

    public event EventHandler? OnConnected;
    public event EventHandler? OnDisconnected;

    public bool IsConnected => _tcpClient?.Connected == true && _stream != null;
    public Stream? Stream => _stream;

    public TcpConnectionManager(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await _connectionSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
            {
                return;
            }

            CleanUpCurrentConnection();

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_host, _port, cancellationToken);
            _stream = _tcpClient.GetStream();

            OnConnected?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    public void StartReading(Func<PipeReader, CancellationToken, Task> readLoopFactory, CancellationToken cancellationToken)
    {
        _readLoopFactory = readLoopFactory;
        StartReadLoop(cancellationToken);
    }

    // Starts a read loop, but at most one at a time. ConnectAsync is safe to call repeatedly (a
    // load test shares a single client), yet every call must NOT spawn an extra PipeReader over the
    // same NetworkStream: concurrent socket reads interleave the byte stream, so one loop can read
    // payload bytes as the 4-byte length header (e.g. 0x48B90000) and the payload deserialization
    // then fails with "Length header size is larger than buffer size".
    private void StartReadLoop(CancellationToken cancellationToken)
    {
        if (_managerCts.IsCancellationRequested || _stream is null || _readLoopFactory is null)
        {
            return;
        }

        if (Interlocked.Exchange(ref _readLoopActive, 1) == 1)
        {
            return;
        }

        _loopCts?.Dispose();
        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _managerCts.Token);

        var pipeReader = PipeReader.Create(_stream);
        _ = RunReadLoopWithReconnectAsync(pipeReader, _loopCts.Token);
    }

    public void ForceReconnect()
    {
        if (_isDisposed)
        {
            return;
        }

        OnDisconnected?.Invoke(this, EventArgs.Empty);
        CleanUpCurrentConnection();

        if (_readLoopFactory != null && !_managerCts.IsCancellationRequested)
        {
            _ = StartReconnectLoopAsync(_managerCts.Token);
        }
    }

    private async Task RunReadLoopWithReconnectAsync(PipeReader reader, CancellationToken token)
    {
        bool isFaulted = false;
        try
        {
            if (_readLoopFactory != null)
            {
                await _readLoopFactory(reader, token);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            isFaulted = true;
        }
        finally
        {
            await reader.CompleteAsync();

            // Снять флаг "чтение активно" до ForceReconnect, чтобы StartReadLoop смог запустить
            // новый цикл (и только один) после реконнекта.
            Interlocked.Exchange(ref _readLoopActive, 0);

            // Если чтение упало или завершилось не по причине Dispose/планового закрытия
            if ((isFaulted || !token.IsCancellationRequested) && !_isDisposed)
            {
                ForceReconnect();
            }
        }
    }

    private async Task StartReconnectLoopAsync(CancellationToken token)
    {
        while (!_isDisposed && !token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_reconnectDelay, token);
                await ConnectAsync(token);

                // Перезапускаем чтение при успешном реконнекте (StartReadLoop сам гарантирует,
                // что запущен ровно один цикл чтения).
                if (IsConnected)
                {
                    StartReadLoop(_managerCts.Token);
                }

                break;
            }
            catch
            {
                // Игнорируем ошибки сетевого подключения и пробуем снова на следующем витке цикла
            }
        }
    }

    private void CleanUpCurrentConnection()
    {
        _stream?.Dispose();
        _stream = null;
        _tcpClient?.Dispose();
        _tcpClient = null;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        _managerCts.Cancel();
        _managerCts.Dispose();

        _loopCts?.Cancel();
        _loopCts?.Dispose();

        CleanUpCurrentConnection();
        _connectionSemaphore.Dispose();
    }
}
