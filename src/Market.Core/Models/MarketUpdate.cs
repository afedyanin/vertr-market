using System.Runtime.InteropServices;

namespace Market.Core.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct MarketUpdate
{
    public readonly long MicrosecondTimestamp;      // 8 байт
    public readonly decimal Price;                  // 16 байт
    public readonly uint Volume;                    // 4 байта
    public readonly ushort AssetId;                 // 2 байта (НОВЫЙ ПОЛЕ: ID актива)
    public readonly byte Side;                      // 1 байт (0 = Bid, 1 = Ask)
    public readonly MarketUpdateType UpdateType;    // 1 byte  (Strongly-typed data state)

    public bool IsTrade => UpdateType == MarketUpdateType.TradeTick;

    public MarketUpdate(
        long timestamp,
        decimal price,
        uint volume,
        ushort assetId,
        byte side,
        MarketUpdateType updateType)
    {
        MicrosecondTimestamp = timestamp;
        Price = price;
        Volume = volume;
        AssetId = assetId;
        Side = side;
        UpdateType = updateType;
    }
}

public enum MarketUpdateType : byte
{
    DepthModify = 0, // Add new liquidity or modify existing price level volume
    TradeTick = 1,   // A real execution transaction took place on the exchange
    DepthDelete = 2  // Completely purge the specific price level from the order book
}