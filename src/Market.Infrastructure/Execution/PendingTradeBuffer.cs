using System.Collections.Concurrent;
using Market.Core.Models;

namespace Market.Infrastructure.Execution;

/// <summary>
/// Буфер сделок, которые пришли раньше, чем стал известен маппинг
/// <c>OrderId -> StrategyId</c>.
/// </summary>
/// <remarks>
/// <para>
/// Каждая сделка извлекается ровно один раз. Сценарий гонки:
/// </para>
/// <list type="number">
/// <item>поток сделок не нашёл маппинг и добавляет сделку в буфер;</item>
/// <item>поток исполнения публикует маппинг и снимает запись из буфера;</item>
/// <item>поток сделок перепроверяет маппинг: если он уже появился, сливает
/// собственный <see cref="PendingTradeEntry"/>.</item>
/// </list>
/// <para>
/// Поэтому <see cref="Add"/> возвращает handle, а <see cref="Drain"/> умеет
/// сливать его независимо от того, осталась ли запись в словаре.
/// </para>
/// </remarks>
public sealed class PendingTradeBuffer
{
    private readonly ConcurrentDictionary<string, PendingTradeEntry> _entries;
    private readonly TimeSpan _ttl;
    private readonly TimeProvider _timeProvider;

    public PendingTradeBuffer(TimeSpan ttl, TimeProvider? timeProvider = null)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), ttl, "TTL must be positive.");
        }

        _ttl = ttl;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _entries = new ConcurrentDictionary<string, PendingTradeEntry>(StringComparer.Ordinal);
    }

    /// <summary>Количество ордеров, ожидающих маппинга.</summary>
    public int Count => _entries.Count;

    /// <summary>Добавляет сделки и возвращает handle для последующего дренажа.</summary>
    public PendingTradeEntry Add(string orderId, IEnumerable<MyTrade> trades)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        ArgumentNullException.ThrowIfNull(trades);

        var entry = _entries.GetOrAdd(
            orderId,
            static (_, now) => new PendingTradeEntry(now),
            _timeProvider.GetUtcNow());

        entry.Add(trades);
        return entry;
    }

    /// <summary>
    /// Удаляет запись (если она ещё в словаре) и возвращает накопленные сделки.
    /// </summary>
    public IReadOnlyList<MyTrade> TryDrain(string orderId, Guid strategyId)
    {
        if (!_entries.TryRemove(orderId, out var entry))
        {
            return [];
        }

        return Drain(entry, strategyId);
    }

    /// <summary>
    /// Сливает конкретный handle, даже если запись уже удалена из словаря.
    /// </summary>
    public IReadOnlyList<MyTrade> Drain(PendingTradeEntry entry, Guid strategyId)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Drain(strategyId);
    }

    /// <summary>
    /// Удаляет записи старше TTL и возвращает количество выброшенных сделок.
    /// Нужна для сделок по ордерам, которые сервису неизвестны (ручные ордера,
    /// другие системы на том же счёте).
    /// </summary>
    public int PurgeExpired()
    {
        var now = _timeProvider.GetUtcNow();
        var dropped = 0;

        foreach (var pair in _entries)
        {
            if (now - pair.Value.CreatedAt < _ttl)
            {
                continue;
            }

            if (_entries.TryRemove(pair.Key, out var entry))
            {
                dropped += entry.Count;
            }
        }

        return dropped;
    }
}
