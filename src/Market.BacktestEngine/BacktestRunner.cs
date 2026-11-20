using Market.Core.Models;
using Market.Infrastructure;

namespace Market.BacktestEngine;

/// <summary>
/// Офлайн-исполнение сигналов стратегии по историческим стаканам.
/// </summary>
/// <remarks>
/// Повторяет подход <c>KalmanFilterBasicStrategyTestsBack</c>:
/// сигналы исполняются через <see cref="BacktestOrderExecution"/>, фактическая
/// позиция возвращается в стратегию через <see cref="IPositionReconcilable"/>,
/// а в конце открытые позиции закрываются по последнему пригодному стакану.
/// </remarks>
public static class BacktestRunner
{
    public static async Task<BacktestRunResult> RunAsync(
        BacktestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var algo = request.Algo
            ?? throw new ArgumentNullException(nameof(request), "Algo must be specified.");

        var portfolio = new Portfolio { StrategyId = algo.StrategyId };
        var execution = new BacktestOrderExecution(request.ExecutionOptions);

        OrderBookDepth? lastValidBookX = null;
        OrderBookDepth? lastValidBookY = null;
        var processedBooks = 0;
        var signalCount = 0;

        await foreach (var book in request.Books.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (!IsInRange(book.Timestamp, request.FromUtc, request.ToUtc))
            {
                continue;
            }

            processedBooks++;

            if (book.AssetId == algo.AssetX && IsUsable(book))
            {
                lastValidBookX = book;
            }
            else if (book.AssetId == algo.AssetY && IsUsable(book))
            {
                lastValidBookY = book;
            }

            var signal = algo.ProcessBook(book);

            if (signal is null)
            {
                continue;
            }

            signalCount++;
            Apply(portfolio, execution.Execute(signal));
            ReconcilePosition(algo, portfolio);
        }

        var liquidationTrades = CloseOpenPositions(
            algo,
            portfolio,
            request.ExecutionOptions,
            lastValidBookX,
            lastValidBookY);

        Apply(portfolio, liquidationTrades);
        ReconcilePosition(algo, portfolio);

        return new BacktestRunResult(
            portfolio,
            processedBooks,
            signalCount,
            portfolio.Trades.Length,
            liquidationTrades.Length);
    }

    private static MyTrade[] CloseOpenPositions(
        IPairTradingAlgo algo,
        Portfolio portfolio,
        BacktestExecutionOptions options,
        OrderBookDepth? lastBookX,
        OrderBookDepth? lastBookY)
    {
        var xQtyLots = -ToLots(portfolio.Get(algo.AssetX)?.Quantity ?? 0m);
        var yQtyLots = -ToLots(portfolio.Get(algo.AssetY)?.Quantity ?? 0m);

        if (xQtyLots == 0 && yQtyLots == 0)
        {
            return [];
        }

        if (lastBookX is null || lastBookY is null)
        {
            throw new BacktestConfigurationException(
                "Не удалось закрыть открытые позиции: нет последнего пригодного стакана.");
        }

        // Для ликвидации сохраняем издержки и дробление, но снимаем
        // ограничение доли уровня, чтобы позиция закрылась полностью.
        var liquidationOptions = new BacktestExecutionOptions
        {
            SlippagePercent = options.SlippagePercent,
            CommissionPercent = options.CommissionPercent,
            MaxTradeLots = options.MaxTradeLots,
        };

        var liquidation = new PairTradingSignal
        {
            StrategyId = algo.StrategyId,
            BookX = MarketBookForLiquidation(lastBookX, xQtyLots),
            BookY = MarketBookForLiquidation(lastBookY, yQtyLots),
            XQtyLots = xQtyLots,
            YQtyLots = yQtyLots,
        };

        return new BacktestOrderExecution(liquidationOptions).Execute(liquidation);
    }

    private static OrderBookDepth MarketBookForLiquidation(OrderBookDepth book, int requiredLots)
    {
        var size = (double)Math.Max(1m, Math.Abs((decimal)requiredLots));
        var bid = book.BestBid;
        var ask = book.BestAsk;

        if (bid is not > 0 || ask is not > 0)
        {
            throw new BacktestConfigurationException(
                $"Не удалось закрыть позицию по активу {book.AssetId}: в последнем стакане нет цен.");
        }

        return new OrderBookDepth(
            book.Timestamp,
            book.AssetId,
            [new OrderBookLevel(bid.Value, size)],
            [new OrderBookLevel(ask.Value, size)]);
    }

    private static void Apply(Portfolio portfolio, IEnumerable<MyTrade> trades)
    {
        foreach (var trade in trades)
        {
            portfolio.Update(trade);
        }
    }

    private static void ReconcilePosition(IPairTradingAlgo algo, Portfolio portfolio)
    {
        if (algo is not IPositionReconcilable reconcilable)
        {
            return;
        }

        reconcilable.ReconcilePosition(
            ToLots(portfolio.Get(algo.AssetX)?.Quantity ?? 0m),
            ToLots(portfolio.Get(algo.AssetY)?.Quantity ?? 0m));
    }

    private static int ToLots(decimal quantity)
        => decimal.ToInt32(decimal.Round(quantity, 0, MidpointRounding.ToZero));

    private static bool IsUsable(OrderBookDepth book)
        => book.IsValid && book.BestBid is > 0 && book.BestAsk is > 0;

    private static bool IsInRange(DateTime timestamp, DateTime? fromUtc, DateTime? toUtc)
    {
        var utc = ToUtc(timestamp);
        return (fromUtc is null || utc >= fromUtc.Value)
            && (toUtc is null || utc <= toUtc.Value);
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
