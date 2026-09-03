using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Market.Core.Models;

/// <summary>
/// Срез стакана на ТОП-10 уровней ликвидности.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct MarketDepth
{
    public readonly ushort AssetId;            // 2 байта
    public readonly long MicrosecondTimestamp; // 8 байт

    // Быстрый доступ к лучшим ценам (Top of the Book) для простых стратегий
    public readonly decimal BestBidPrice;      // 16 байт
    public readonly decimal BestAskPrice;      // 16 байт
    public readonly uint BestBidVolume;        // 4 байта
    public readonly uint BestAskVolume;        // 4 байта

    // Фиксированные буферы на 10 уровней (Inline Arrays)
    // Занимают 200 байт на Bids и 200 байт на Asks внутри структуры
    private readonly DepthBuffer10 _bidsBuffer;
    private readonly DepthBuffer10 _asksBuffer;

    public MarketDepth(ushort assetId, long timestamp, ref readonly DepthBuffer10 bids, ref readonly DepthBuffer10 asks)
    {
        AssetId = assetId;
        MicrosecondTimestamp = timestamp;
        _bidsBuffer = bids;
        _asksBuffer = asks;

        // Кэшируем Top of the Book для O(1) доступа без обращения к буферу
        BestBidPrice = bids[0].Price;
        BestBidVolume = bids[0].Volume;
        BestAskPrice = asks[0].Price;
        BestAskVolume = asks[0].Volume;
    }

    /// <summary>
    /// Безопасный доступ к уровням цен Bids в виде Span без копирования данных.
    /// </summary>
    public ReadOnlySpan<PriceLevel> GetBids()
    {
        // Автоматически преобразует встроенный буфер в Span для итерации через foreach/for
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _bidsBuffer[0]), 10);
    }

    /// <summary>
    /// Безопасный доступ к уровням цен Asks в виде Span без копирования данных.
    /// </summary>
    public ReadOnlySpan<PriceLevel> GetAsks()
    {
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _asksBuffer[0]), 10);
    }
}

/// <summary>
/// Объявление фиксированного буфера (Inline Array) на 10 элементов PriceLevel.
/// Этот тип разворачивается компилятором как плоская структура из 10 элементов подряд в памяти (200 байт).
/// </summary>
[InlineArray(10)]
public struct DepthBuffer10
{
    private PriceLevel _element0;
}