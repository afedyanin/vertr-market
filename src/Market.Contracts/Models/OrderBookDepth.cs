using MessagePack;

namespace Market.Contracts.Models;

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
