using System.Runtime.InteropServices;

namespace Market.Core.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct TradeTick
{
    public readonly long MicrosecondTimestamp;      // 8 байт
    public readonly decimal Price;                  // 16 байт
    public readonly uint Volume;                    // 4 байта
    public readonly ushort AssetId;                 // 2 байта
    public readonly byte Side;                      // 1 байт (0 = Bid, 1 = Ask)

    public TradeTick(
        long timestamp,
        decimal price,
        uint volume,
        ushort assetId,
        byte side)
    {
        MicrosecondTimestamp = timestamp;
        Price = price;
        Volume = volume;
        AssetId = assetId;
        Side = side;
    }
}
