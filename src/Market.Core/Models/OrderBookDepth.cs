using MessagePack;

namespace Market.Core.Models;

[MessagePackObject]
public class OrderBookDepth
{
    [Key(0)]
    public DateTime Timestamp { get; }

    [Key(1)]
    public ushort AssetId { get; }

    [Key(2)]
    public OrderBookLevel[] Bids { get; } // Покупатели (сортировка от лучшей/высшей к худшей)

    [Key(3)]
    public OrderBookLevel[] Asks { get; } // Продавцы (сортировка от лучшей/низшей к худшей)

    [IgnoreMember]
    public double? BestBid => Bids is { Length: > 0 } ? Bids[0].Price : null;

    [IgnoreMember]
    public double? BestAsk => Asks is { Length: > 0 } ? Asks[0].Price : null;

    [IgnoreMember]
    public double? MidPrice => (BestBid.HasValue && BestAsk.HasValue)
        ? (BestBid.Value + BestAsk.Value) / 2.0
        : null;

    public OrderBookDepth(
        DateTime timestamp,
        ushort assetId,
        OrderBookLevel[] bids,
        OrderBookLevel[] asks)
    {
        Timestamp = timestamp;
        AssetId = assetId;
        Bids = bids;
        Asks = asks;
    }
}

[MessagePackObject]
public readonly struct OrderBookLevel
{
    [Key(0)]
    public double Price { get; }

    [Key(1)]
    public double Size { get; }

    public OrderBookLevel(double price, double size)
    {
        Price = price;
        Size = size;
    }
}
