using Market.Core.Models;

namespace Market.BacktestEngine;

/// <summary>
/// Результат прогона: портфель и базовая статистика.
/// </summary>
public sealed record BacktestRunResult(
    Portfolio Portfolio,
    int ProcessedBooks,
    int SignalCount,
    int TradeCount,
    int LiquidationTradeCount);
