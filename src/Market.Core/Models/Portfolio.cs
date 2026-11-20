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

    /// <summary>
    /// Суммарная комиссия по всем сделкам портфеля.
    /// </summary>
    public decimal Commission { get; private set; } = 0;

    public Position? Get(ushort assetId)
    {
        _ = _positions.TryGetValue(assetId, out var position);
        return position;
    }

    public Position Update(MyTrade trade)
    {
        var position = _positions.GetOrAdd(trade.AssetId, new Position
        {
            AssetId = trade.AssetId,
        });

        // Сначала применяем сделку к позиции: если она невалидна, не
        // оставляем её в истории и не начисляем комиссию.
        position.Update(trade.Qty, trade.Price, trade.Commission);

        _trades.Add(trade);
        Commission += trade.Commission;
        return position;
    }
}
