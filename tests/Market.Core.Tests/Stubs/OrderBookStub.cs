using Market.Core.Models;

namespace Market.Core.Tests.Stubs;

internal static class OrderBookStub
{
    public static OrderBookDepth Create(DateTime timestamp, ushort assetId, double bidPrice, double askPrice)
        => new OrderBookDepth(timestamp, assetId, [new OrderBookLevel(bidPrice, 1)], [new OrderBookLevel(askPrice, 1)]);
}
