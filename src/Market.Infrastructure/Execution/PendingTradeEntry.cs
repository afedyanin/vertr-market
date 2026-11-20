using System.Collections.Concurrent;
using Market.Core.Models;

namespace Market.Infrastructure.Execution;

/// <summary>
/// Накопленные сделки одного ордера, ожидающие публикации маппинга
/// <c>OrderId -> StrategyId</c>.
/// </summary>
/// <remarks>
/// Экземпляр не создаётся напрямую: его возвращает <see cref="PendingTradeBuffer.Add"/>,
/// чтобы вызывающая сторона могла слить собственный handle даже после того,
/// как запись уже удалена из словаря.
/// </remarks>
public sealed class PendingTradeEntry
{
    private readonly ConcurrentQueue<MyTrade> _trades = new();

    internal PendingTradeEntry(DateTimeOffset createdAt)
    {
        CreatedAt = createdAt;
    }

    /// <summary>Момент создания записи, используется для TTL-очистки.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Количество ещё не извлечённых сделок.</summary>
    public int Count => _trades.Count;

    internal void Add(IEnumerable<MyTrade> trades)
    {
        foreach (var trade in trades)
        {
            _trades.Enqueue(trade);
        }
    }

    internal IReadOnlyList<MyTrade> Drain(Guid strategyId)
    {
        var drained = new List<MyTrade>(_trades.Count);

        while (_trades.TryDequeue(out var trade))
        {
            drained.Add(trade with { StrategyId = strategyId });
        }

        return drained;
    }
}
