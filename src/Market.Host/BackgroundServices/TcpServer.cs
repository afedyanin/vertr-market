using System.Diagnostics;
using System.Diagnostics.Metrics;
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
    private readonly ActivitySource _activitySource;
    private readonly Counter<long> _commandCounter;
    private readonly Histogram<double> _commandDurationHistogram;

    private readonly ILogger<TcpServer> _logger;

#pragma warning disable CA2213 // Disposable fields should be disposed
    private readonly ILoggerFactory _loggerFactory;
#pragma warning restore CA2213 // Disposable fields should be disposed

    private readonly IObjectStore<MarketDepth> _bookStore;

    private readonly MarketApiSettings _settings;
    private readonly SemaphoreSlim _semaphore;

    private readonly int _port;
    private Socket? _listenSocket;
    private bool _disposed;

    public TcpServer(
        ActivitySource activitySource,
        Meter meter,
        IServiceProvider serviceProvider,
        IOptions<MarketApiSettings> options)
    {
        _settings = options.Value;
        _port = _settings.TcpPort;

        _activitySource = activitySource;
        _commandCounter = meter.CreateCounter<long>("commands.total");
        _commandDurationHistogram = meter.CreateHistogram<double>("commands.duration");

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
        socket.NoDelay = true;

        try
        {
            var pipe = SocketExtensions.CreatePipes(socket);
            using var pipeStream = pipe.Stream;

            using var parser = new TcpCommandParser(
                _bookStore,
                pipe.Output,
                _loggerFactory,
                _activitySource,
                _commandCounter,
                _commandDurationHistogram);

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

        _listenSocket?.Dispose();
        _semaphore.Dispose();

        _disposed = true;
    }
}

internal static class SocketExtensions
{
    private const int PipeBufferSize = 64 * 1024;

    public static (PipeReader Input, PipeWriter Output, NetworkStream Stream) CreatePipes(Socket socket)
    {
        var stream = new NetworkStream(socket, ownsSocket: false);
        var input = PipeReader.Create(stream, new StreamPipeReaderOptions(pool: null, bufferSize: PipeBufferSize, minimumReadSize: 1024, leaveOpen: true));
        var output = PipeWriter.Create(stream, new StreamPipeWriterOptions(pool: null, minimumBufferSize: PipeBufferSize, leaveOpen: true));

        return (input, output, stream);
    }
}
