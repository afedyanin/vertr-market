using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Market.Core.Models;

public class Portfolio
{
    private readonly ConcurrentDictionary<ushort, Position> _positions = [];

    public int StrategyId { get; init; }

    public ImmutableArray<Position> Positions => [.. _positions.Values];

    public Position? Get(ushort assetId)
    {
        _ = _positions.TryGetValue(assetId, out var position);
        return position;
    }

    public Position Update(ushort assetId, decimal tradeQty, decimal tradePrice)
    {
        var position = _positions.GetOrAdd(assetId, new Position
        {
            AssetId = assetId,
        });

        position.Update(tradeQty, tradePrice);
        return position;
    }
}
