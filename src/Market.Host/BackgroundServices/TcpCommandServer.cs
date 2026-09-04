using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Market.Core.Tcp;

namespace Market.Host.BackgroundServices;

public class TcpCommandServer : BackgroundService
{
    private readonly int _port;
    private readonly ILogger<TcpCommandServer> _logger;
    private Socket? _listenSocket;

    public TcpCommandServer(int port, ILogger<TcpCommandServer> logger)
    {
        _port = port;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Создаем сокет, оптимизированный под IPv4 и TCP
        _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
        _listenSocket.Listen(100);

        _logger.LogInformation("TCP Сервер запущен на порту {Port}. Ожидание подключений...", _port);

        // Регистрируем закрытие сокета при отмене токена, чтобы принудительно прервать AcceptAsync
        using var registration = stoppingToken.Register(() => _listenSocket?.Close());

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Ожидаем новое подключение клиента асинхронно
                Socket clientSocket = await _listenSocket.AcceptAsync(stoppingToken);
                _logger.LogInformation("Клиент подключен: {RemoteEndPoint}", clientSocket.RemoteEndPoint);

                // Обрабатываем каждого клиента в отдельной задаче, передавая токен отмены
                _ = ProcessClientAsync(clientSocket, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Корректный выход при остановке сервиса хостом
            _logger.LogInformation("Работа TCP Сервера останавливается...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка в цикле приема подключений TCP сервера.");
        }
        finally
        {
            _listenSocket?.Close();
            _logger.LogInformation("TCP Сервер окончательно остановлен.");
        }
    }

    private async Task ProcessClientAsync(Socket socket, CancellationToken stoppingToken)
    {
        // Обертка сокета в Pipe
        IDuplexPipe pipe = SocketExtensions.CreatePipe(socket);
        var parser = new TcpCommandParser(pipe.Output);

        try
        {
            // Передаем PipeReader сокета в ваш метод парсинга
            await parser.ReadPipeAsync(pipe.Input, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Сбой при обработке данных клиента {RemoteEndPoint}", socket.RemoteEndPoint);
        }
        finally
        {
            _logger.LogInformation("Клиент отключился: {RemoteEndPoint}", socket.RemoteEndPoint);

            // Корректно закрываем пайпы и освобождаем ресурсы сокета
            await pipe.Input.CompleteAsync();
            await pipe.Output.CompleteAsync();

            if (socket.Connected)
            {
                socket.Shutdown(SocketShutdown.Both);
            }

            socket.Close();
        }
    }

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public override void Dispose()
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
    {
        base.Dispose();
        _listenSocket?.Dispose();
    }
}

// Вспомогательный класс для работы с пайпами поверх NetworkStream
public static class SocketExtensions
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
