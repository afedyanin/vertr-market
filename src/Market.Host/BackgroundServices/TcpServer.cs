using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Market.ApiClient;
using Market.Core.Abstractions;
using Market.Core.Models;
using Market.Core.Tcp;
using Microsoft.Extensions.Options;

namespace Market.Host.BackgroundServices;

public class TcpServer : BackgroundService
{
    private readonly ILogger<TcpServer> _logger;

    // Owned by the DI container, not by TcpServer; intentionally never disposed here.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2213:Non-disposed field", Justification = "ILoggerFactory is owned by the DI container, not by TcpServer.")]
    private readonly ILoggerFactory _loggerFactory;

    private readonly IObjectStore<MarketDepth> _bookStore;

    private readonly MarketApiSettings _settings;
    private readonly SemaphoreSlim _semaphore;

    private readonly int _port;
    private Socket? _listenSocket;
    private bool _disposed;

    public TcpServer(
        IServiceProvider serviceProvider,
        IOptions<MarketApiSettings> options)
    {
        _settings = options.Value;
        _port = _settings.TcpPort;

        _bookStore = serviceProvider.GetRequiredService<IObjectStore<MarketDepth>>();
        _loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        _logger = _loggerFactory.CreateLogger<TcpServer>();
        _semaphore = new SemaphoreSlim(_settings.MaxConcurrentConnections, _settings.MaxConcurrentConnections);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.UseTcp)
        {
            _logger.LogInformation("TCP server is disabled.");
            return;
        }

        _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
        _listenSocket.Listen();

        _logger.LogInformation("TCP server started on {Port}", _port);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var clientSocket = await _listenSocket.AcceptAsync(stoppingToken);
                _logger.LogInformation("Client connected: {RemoteEndPoint}", clientSocket.RemoteEndPoint);

                _ = ProcessClientAsync(clientSocket, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("TCP server stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical exception in TCP server");
        }
        finally
        {
            _listenSocket?.Close();
            _logger.LogInformation("TCP server stopped.");
        }
    }

    private async Task ProcessClientAsync(
        Socket socket,
        CancellationToken stoppingToken)
    {
        // Acquire the slot before the try so the finally below always runs and releases it. If
        // WaitAsync is itself cancelled (shutting down while a client is queued) there is no
        // finally to run, which is correct because no slot was acquired.
        await _semaphore.WaitAsync(stoppingToken);

        // Nagle off on both ends: in a request/response protocol with small replies, Nagle's
        // algorithm combined with delayed ACK can stall every response by a round trip
        // (up to ~200 ms).
        socket.NoDelay = true;

        try
        {
            var pipe = SocketExtensions.CreatePipes(socket);

            // The NetworkStream is created with ownsSocket:false, so disposing it releases its
            // buffer without closing the socket (the socket is closed separately below).
            using var pipeStream = pipe.Stream;

            using var parser = new TcpCommandParser(_bookStore, pipe.Output, _loggerFactory);

            try
            {
                await parser.ReadPipeAsync(pipe.Input, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data processing failure: {RemoteEndPoint}. Message={Message}", socket.RemoteEndPoint, ex.Message);
            }
            finally
            {
                _logger.LogInformation("Client disconnected: {RemoteEndPoint}. ({Commands}) commands processed.", socket.RemoteEndPoint, parser.CommandsProcessed);

                await pipe.Input.CompleteAsync();
                await pipe.Output.CompleteAsync();
            }
        }
        finally
        {
            // Shutdown can throw if the peer already closed; it must not prevent Close/Release.
            try
            {
                if (socket.Connected)
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Socket shutdown failed for {RemoteEndPoint}", socket.RemoteEndPoint);
            }

            socket.Close();
            _semaphore.Release();
        }
    }

    public override void Dispose()
    {
        base.Dispose();

        if (_disposed)
        {
            return;
        }

        // Do not dispose _loggerFactory: it is an application-wide service owned by the DI
        // container, not by TcpServer. Disposing it here would break every other logger.
        _listenSocket?.Dispose();
        _semaphore.Dispose();

        _disposed = true;
    }
}

internal static class SocketExtensions
{
    public static (PipeReader Input, PipeWriter Output, NetworkStream Stream) CreatePipes(Socket socket)
    {
        // The .NET 10 in-box System.IO.Pipelines pipes a Stream (not a raw Socket). Pipes use
        // the stream's async I/O, which for NetworkStream goes straight to the socket without
        // its 8 KB sync buffer, so no extra copy is added.
        //
        // The reader default buffer is 4 KB, but a depth response for a few dozen books is
        // much larger, so a bigger buffer means fewer socket read/write round trips per
        // payload. minimumReadSize is the default 1 KB (it must be > 0; it only controls
        // when a new buffer segment is allocated, not when ReadAsync returns).
        var stream = new NetworkStream(socket, ownsSocket: false);

        var input = PipeReader.Create(stream, new StreamPipeReaderOptions(pool: null, bufferSize: PipeBufferSize, minimumReadSize: 1024, leaveOpen: true));
        var output = PipeWriter.Create(stream, new StreamPipeWriterOptions(pool: null, minimumBufferSize: PipeBufferSize, leaveOpen: true));

        return (input, output, stream);
    }

    private const int PipeBufferSize = 64 * 1024;
}
