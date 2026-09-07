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

        try
        {
            using var pipe = SocketExtensions.CreatePipe(socket);
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
    public static StreamDuplexPipe CreatePipe(Socket socket)
    {
        var stream = new NetworkStream(socket, ownsSocket: false);
        return new StreamDuplexPipe(stream);
    }

    internal sealed class StreamDuplexPipe : IDuplexPipe, IDisposable
    {
        private readonly NetworkStream _stream;

        public PipeReader Input { get; }
        public PipeWriter Output { get; }

        public StreamDuplexPipe(NetworkStream stream)
        {
            _stream = stream;
            Input = PipeReader.Create(stream);
            Output = PipeWriter.Create(stream);
        }

        // The NetworkStream was created with ownsSocket:false, so disposing it releases its
        // buffer without closing the socket (the socket is closed separately by TcpServer).
        public void Dispose() => _stream.Dispose();
    }
}
