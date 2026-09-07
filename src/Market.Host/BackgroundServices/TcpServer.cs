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
        await _semaphore.WaitAsync(stoppingToken);

        var pipe = SocketExtensions.CreatePipe(socket);
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

            if (socket.Connected)
            {
                socket.Shutdown(SocketShutdown.Both);
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

        _loggerFactory?.Dispose();
        _listenSocket?.Dispose();
        _semaphore.Dispose();

        _disposed = true;
    }
}

internal static class SocketExtensions
{
    public static IDuplexPipe CreatePipe(Socket socket)
    {
        var stream = new NetworkStream(socket, ownsSocket: false);
        return new StreamDuplexPipe(stream);
    }

    private sealed class StreamDuplexPipe : IDuplexPipe
    {
        public PipeReader Input { get; }
        public PipeWriter Output { get; }

        public StreamDuplexPipe(NetworkStream stream)
        {
            Input = PipeReader.Create(stream);
            Output = PipeWriter.Create(stream);
        }
    }
}
