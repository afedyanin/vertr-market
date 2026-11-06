using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Market.Core.Models;

public class Portfolio
{
    private readonly ConcurrentDictionary<ushort, Position> _positions = [];
    private readonly IList<MyTrade> _trades = [];

    public Guid StrategyId { get; init; }

    public ImmutableArray<Position> Positions => [.. _positions.Values];

    public ImmutableArray<MyTrade> Trades => [.. _trades];

    public Position? Get(ushort assetId)
    {
        _ = _positions.TryGetValue(assetId, out var position);
        return position;
    }

    public Position Update(MyTrade trade)
    {
        _trades.Add(trade);

        var position = _positions.GetOrAdd(trade.AssetId, new Position
        {
            AssetId = trade.AssetId,
        });

        position.Update(trade.Qty, trade.Price);
        return position;
    }
}
