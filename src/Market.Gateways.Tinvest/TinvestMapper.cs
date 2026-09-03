using System.Runtime.CompilerServices;
using Market.Core.Models;
using Tinkoff.InvestApi.V1;

namespace Market.Gateways.Tinvest;

internal static class TinvestMapper
{
    /// <summary>
    /// Конвертирует Protobuf OrderBook в нативный MarketDepth без аллокаций в куче.
    /// </summary>
    /// <param name="orderBook">Входящий объект стакана из gRPC стрима</param>
    /// <param name="assetId">Внутренний числовой идентификатор инструмента (ускоряет работу вместо строк)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] // Подсказываем JIT встроить метод для максимальной скорости
    public static MarketDepth ToMarketDepth(OrderBook orderBook, ushort assetId)
    {
        // 1. Извлекаем Unix Microseconds из Google Protobuf Timestamp
        long microsecondTimestamp = 0;
        if (orderBook.Time != null)
        {
            microsecondTimestamp = (orderBook.Time.Seconds * 1_000_000) + (orderBook.Time.Nanos / 1_000);
        }

        // 2. Создаем пустые фиксированные буферы на стеке
        var bidsBuffer = new DepthBuffer10();
        var asksBuffer = new DepthBuffer10();

        // 3. Заполняем Bids (Покупки). 
        // По спецификации Tinkoff API они могут идти вразнобой, поэтому здесь предполагается, 
        // что для стратегий ТОП-10 они уже должны быть отсортированы брокером или вашим кодом.
        // Берем максимум 10 элементов, чтобы не выйти за пределы InlineArray.
        var bidsCount = Math.Min(orderBook.Bids.Count, 10);
        for (var i = 0; i < bidsCount; i++)
        {
            var protoBid = orderBook.Bids[i];

            // Записываем напрямую в InlineArray по индексу
            bidsBuffer[i] = new PriceLevel(
                price: protoBid.Price,
                volume: (uint)protoBid.Quantity
            );
        }

        // 4. Заполняем Asks (Продажи).
        var asksCount = Math.Min(orderBook.Asks.Count, 10);
        for (var i = 0; i < asksCount; i++)
        {
            var protoAsk = orderBook.Asks[i];

            asksBuffer[i] = new PriceLevel(
                price: protoAsk.Price,
                volume: (uint)protoAsk.Quantity
            );
        }

        // 5. Собираем и возвращаем MarketDepth (передача буферов идет по readonly-ссылке `ref readonly`)
        return new MarketDepth(assetId, microsecondTimestamp, ref bidsBuffer, ref asksBuffer);
    }

    /// <summary>
    /// Конвертирует Protobuf Trade в нативную структуру MarketUpdate без аллокаций в куче.
    /// </summary>
    /// <param name="protoTrade">Входящий объект сделки из gRPC стрима</param>
    /// <param name="assetId">Внутренний числовой идентификатор инструмента</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MarketUpdate ToMarketUpdate(Trade protoTrade, ushort assetId)
    {
        // 1. Извлекаем Unix Microseconds из Google Protobuf Timestamp
        long microsecondTimestamp = 0;
        if (protoTrade.Time != null)
        {
            microsecondTimestamp = (protoTrade.Time.Seconds * 1_000_000) + (protoTrade.Time.Nanos / 1_000);
        }

        // 2. Безопасное приведение направления сделки к byte (Enum из Protobuf)
        var side = (byte)(protoTrade.Direction == TradeDirection.Sell ? 1 : 0);

        // 3. Собираем структуру на стеке
        return new MarketUpdate(
            timestamp: microsecondTimestamp,
            price: protoTrade.Price,
            volume: (uint)protoTrade.Quantity,
            assetId: assetId,
            side: side,
            updateType: MarketUpdateType.TradeTick
        );
    }
}

