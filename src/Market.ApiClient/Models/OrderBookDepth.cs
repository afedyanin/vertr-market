using MessagePack;

namespace Market.ApiClient.Models;

[MessagePackObject]
public class OrderBookDepth
{
    [Key(0)]
    public ushort AssetId { get; }

    [Key(1)]
    public OrderBookLevel[] Bids { get; } // Покупатели (сортировка от лучшей/высшей к худшей)

    [Key(2)]
    public OrderBookLevel[] Asks { get; } // Продавцы (сортировка от лучшей/низшей к худшей)

    public OrderBookDepth(ushort assetId, OrderBookLevel[] bids, OrderBookLevel[] asks)
    {
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
