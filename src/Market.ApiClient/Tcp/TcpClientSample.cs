using Market.ApiClient.Dtos;
using Market.ApiClient.Tcp.Internals;

namespace Market.ApiClient.Tcp;

internal static class TcpClientSample
{
    private static ITcpApiClient? _tcpClient;
    private static IMarketTcpApiClient? _сlient;

    static async Task Main(string[] args)
    {
        Console.WriteLine("[Клиент] Инициализация компонентов...");

        _tcpClient = new TcpApiClient("127.0.0.1", 8080);
        _сlient = new MarketTcpApiClient(_tcpClient);

        // 2. Подписываемся на события изменения статуса соединения
        _tcpClient.OnConnected += OnClientConnected;
        _tcpClient.OnDisconnected += OnClientDisconnected;

        try
        {
            Console.WriteLine("[Клиент] Попытка установить соединение...");

            // 3. Асинхронно подключаемся к серверу
            await _tcpClient.ConnectAsync();

            // Небольшая пауза, чтобы симулировать работу и дождаться события OnConnected
            await Task.Delay(1000);

            // 4. Используем бизнес-сервис для получения данных
            Console.WriteLine("\n[Бизнес-логика] Запрос книги заявок для Asset ID: 100...");
            MarketDepthDto[] books = await _сlient.GetBooksAsync(assetId: 100, count: 5);

            Console.WriteLine($"[Бизнес-логика] Получено стаканов: {books.Length}");
            // Здесь может быть обработка полученных данных (например, вывод в консоль)
        }
        catch (TimeoutException ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Ошибка] Время ожидания ответа истекло: {ex.Message}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Критическая ошибка] {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            // 5. Обязательно освобождаем ресурсы перед выходом
            Console.WriteLine("\n[Клиент] Завершение работы приложения...");

            if (_tcpClient != null)
            {
                // Отписываемся от событий, чтобы избежать утечек памяти
                _tcpClient.OnConnected -= OnClientConnected;
                _tcpClient.OnDisconnected -= OnClientDisconnected;

                // Закрываем соединение и очищаем ресурсы
                _tcpClient.Dispose();
                Console.WriteLine("[Клиент] Ресурсы успешно освобождены.");
            }
        }

        Console.WriteLine("Нажмите любую клавишу для выхода...");
        Console.ReadKey();
    }

    // Обработчик события успешного подключения
    private static void OnClientConnected(object? sender, EventArgs e)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[Событие] Успешно подключено к TCP-серверу!");
        Console.ResetColor();
    }

    // Обработчик события разрыва соединения
    private static void OnClientDisconnected(object? sender, EventArgs e)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("[Событие] Соединение с TCP-сервером разорвано.");
        Console.ResetColor();
    }
}
