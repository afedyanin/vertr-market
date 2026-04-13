using Binance.Net.Clients;
using bybit.net.api.WebSocketStream;

namespace Vertr.Market.ByBit.ConsoleApp;

internal sealed class Program
{
    public static async Task MainByBit(string[] args)
    {
        Console.WriteLine("Запуск Bybit WebSocket клиента...");

        // 1. Инициализация WebSocket клиента для Спотового рынка (Spot)
        // Используем публичный стрим (без ключей API), так как котировки - это публичные данные.
        // Параметр useTestNet: false для реальной биржи.
        var spotWebsocket = new BybitSpotWebSocket(
            apiKey: "",
            apiSecret: "",
            useTestNet: false
        );

        // 2. Обработка входящих сообщений
        // Это событие срабатывает при поступлении любых данных по подписке
        spotWebsocket.OnMessageReceived(
            (data) =>
            {
                // data - это сырой JSON ответ от биржи
                // Для простоты выводим его в консоль. В реальном приложении здесь был бы парсинг JSON.
                Console.WriteLine($"[{DateTime.UtcNow:T}] Новые данные: {data}");
                return Task.CompletedTask;
            },
            CancellationToken.None
        );

        // 3. Формирование списка топиков для подписки
        // Формат для V5 API Spot Tickers: tickers.{symbol}
        var subscriptions = new string[]
        {
                "tickers.BTCUSDT", // Биткоин
                "tickers.ETHUSDT", // Эфириум
                "tickers.SOLUSDT"  // Солана
        };

        try
        {
            Console.WriteLine("Подключение к потоку котировок...");

            // 4. Подключение и подписка
            await spotWebsocket.ConnectAsync(subscriptions, CancellationToken.None);

            Console.WriteLine("Подписка активна. Ожидание данных (Ctrl+C для выхода)...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка подключения: {ex.Message}");
        }

        // 5. Удержание консоли открытой
        // Используем бесконечный цикл или ожидание отмены, чтобы программа не завершилась
        await Task.Delay(Timeout.Infinite);
    }

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Binance WebSocket Client ===");

        // 1. Создаем клиент для работы с WebSocket
        // Для публичных данных (цены) API-ключи не требуются.
        using var socketClient = new BinanceSocketClient();

        // 2. Список торговых пар
        var symbols = new[] { "BTCUSDT", "ETHUSDT", "BNBUSDT" };

        Console.WriteLine($"Подписка на пары: {string.Join(", ", symbols)}...");

        // 3. Подписка на поток "Best Price" (Ticker)
        // BookTicker предоставляет лучшую цену Ask (продажа) и Bid (покупка) мгновенно.
        foreach (var symbol in symbols)
        {
            var subscribeResult = await socketClient.SpotApi.ExchangeData.SubscribeToBookTickerUpdatesAsync(symbol, data =>
            {
                // Данные приходят в объекте data.Data
                var quote = data.Data;
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {quote.Symbol}: " +
                                  $"Best Bid: {quote.BestBidPrice} | " +
                                  $"Best Ask: {quote.BestAskPrice}");
            });

            // Проверка успешности подписки
            if (subscribeResult.Success)
            {
                Console.WriteLine($"Успешно подписались на {symbol}");
            }
            else
            {
                Console.WriteLine($"Ошибка подписки на {symbol}: {subscribeResult.Error}");
            }
        }

        Console.WriteLine("\nПоток данных запущен. Нажмите любую клавишу для выхода...");
        Console.ReadKey();

        // Автоматическое закрытие соединений при выходе из using
    }
}
