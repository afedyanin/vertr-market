using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Market.Core.Tcp;

namespace Market.Host.BackgroundServices;

public class TcpServer : BackgroundService
{
    private readonly int _port;
    private readonly ILogger<TcpServer> _logger;

    private readonly IServiceProvider _serviceProvider;
    private Socket? _listenSocket;

    private bool _disposed;

    public TcpServer(int port,
        IServiceProvider serviceProvider,
        ILogger<TcpServer> logger)
    {
        _port = port;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
        _listenSocket.Listen(100);

        _logger.LogInformation("TCP server started on {Port}", _port);

        using var registration = stoppingToken.Register(() => _listenSocket?.Close());

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Socket clientSocket = await _listenSocket.AcceptAsync(stoppingToken);
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

    private async Task ProcessClientAsync(Socket socket, CancellationToken stoppingToken)
    {
        var pipe = SocketExtensions.CreatePipe(socket);
        using var scope = _serviceProvider.CreateScope();
        using var parser = new TcpCommandParser(scope, pipe.Output);

        try
        {
            await parser.ReadPipeAsync(pipe.Input, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data processing failure: {RemoteEndPoint}", socket.RemoteEndPoint);
        }
        finally
        {
            _logger.LogInformation("Client disconnected: {RemoteEndPoint}", socket.RemoteEndPoint);

            await pipe.Input.CompleteAsync();
            await pipe.Output.CompleteAsync();

            if (socket.Connected)
            {
                socket.Shutdown(SocketShutdown.Both);
            }

            socket.Close();
        }
    }

    public override void Dispose()
    {
        base.Dispose();

        if (_disposed)
        {
            return;
        }

        _listenSocket?.Dispose();
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
