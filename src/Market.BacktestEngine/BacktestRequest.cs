using Market.Core.Models;
using Market.Infrastructure;

namespace Market.BacktestEngine;

/// <summary>
/// Запрос на офлайн-прогон стратегии.
/// </summary>
public sealed record BacktestRequest(
    IPairTradingAlgo Algo,
    IAsyncEnumerable<OrderBookDepth> Books,
    BacktestExecutionOptions ExecutionOptions,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);
