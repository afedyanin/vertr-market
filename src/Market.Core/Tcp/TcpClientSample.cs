namespace Market.Core.Tcp;

internal static class TcpClientSample
{
    static async Task Start(string[] args)
    {
        using var client = new MarketTcpApiClient("127.0.0.1", 5000);

        // Подписываемся на события для логирования
        client.OnConnected += () => Console.WriteLine("[INFO] Сессия TCP успешно установлена.");
        client.OnDisconnected += () => Console.WriteLine("[WARN] Соединение потеряно! Переход в режим ожидания сервера...");

        try
        {
            await client.ConnectAsync();
        }
        catch
        {
            Console.WriteLine("[ERR] Сервер недоступен при старте, но клиент попробует подключиться позже сам.");
            // Инициируем фоновый реконнект, если сервер лежал при первом запуске
            _ = Task.Run(() => client.ConnectAsync());
        }

        // Эмуляция работы
        while (true)
        {
            try
            {
                Console.WriteLine("Запрос данных...");
                var books = await client.GetBooks(assetId: 1, count: 2);
                Console.WriteLine($"Получено стаканов: {books.Length}");
            }
            catch (TimeoutException tex)
            {
                // Сервер завис или не ответил вовремя
                Console.WriteLine($"[TIMEOUT] {tex.Message}");
            }
            catch (Exception ex)
            {
                // Сетевая ошибка (клиент переподключается в этот момент)
                Console.WriteLine($"[ОШИБКА СЕТИ] {ex.Message}");
            }

            await Task.Delay(3000); // Повторяем каждые 3 секунды
        }
    }
}
