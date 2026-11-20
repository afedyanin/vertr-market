using Market.Core.Algos.PairTradingV2;
using Market.Core.FileStore;
using Market.Core.Models;
using Market.Infrastructure;

namespace Market.Core.Tests.Algos.PairTradingV2;

/// <summary>
/// Бэктесты <see cref="KalmanFilterBasicStrategy"/> на записанных стаканах.
///
/// Сигнал исполняется через <see cref="BacktestOrderExecution"/>, а
/// фактическая позиция после каждой сделки передаётся обратно в стратегию
/// через <see cref="KalmanFilterBasicStrategy.ReconcilePosition"/>. В конце
/// прогона открытые позиции закрываются по последнему известному стакану с
/// теми же проскальзыванием и комиссией.
/// </summary>
[TestFixture]
public class KalmanFilterBasicStrategyTestsBack
{
    private const ushort AssetX = 1;
    private const ushort AssetY = 5;

    [TestCase("Data\\books_2026-10-05.bin.gz")]
    [TestCase("Data\\books_2026-10-06.bin.gz")]
    public async Task BackTestSimple(string filePath)
    {
        var result = await RunFileAsync(
            filePath,
            CreateStrategy(),
            new BacktestExecutionOptions());

        PrintResult(filePath, "simple", result);
        AssertCompleted(result);
    }

    [TestCase("Data\\books_2026-10-05.bin.gz")]
    [TestCase("Data\\books_2026-10-06.bin.gz")]
    public async Task BackTestWithCosts(string filePath)
    {
        const double commissionPercent = 0.05;
        const double slippagePercent = 0.02;

        var result = await RunFileAsync(
            filePath,
            CreateStrategy(
                feeRateX: commissionPercent / 100.0,
                feeRateY: commissionPercent / 100.0),
            new BacktestExecutionOptions
            {
                SlippagePercent = slippagePercent,
                CommissionPercent = commissionPercent,
            });

        PrintResult(filePath, "costs", result);
        AssertCompleted(result);

        if (result.TradeCount > 0)
        {
            Assert.That(result.Commission, Is.GreaterThan(0m),
                "При заданной комиссии и исполненных сделках комиссия должна быть начислена");
        }
    }

    [TestCase("Data\\books_2026-10-05.bin.gz")]
    [TestCase("Data\\books_2026-10-06.bin.gz")]
    public async Task BackTestWithPartialFills(string filePath)
    {
        var result = await RunFileAsync(
            filePath,
            CreateStrategy(),
            new BacktestExecutionOptions
            {
                MaxTradeLots = 1,
                MaxLevelParticipation = 0.5,
            });

        PrintResult(filePath, "partial-fills", result);
        AssertCompleted(result);

        Assert.That(result.Trades.All(t => Math.Abs(t.Qty) <= 1m), Is.True,
            "При MaxTradeLots=1 каждый трейд должен быть не больше одного лота");
    }

    [TestCase("Data\\books_2026-10-05.bin.gz")]
    [TestCase("Data\\books_2026-10-06.bin.gz")]
    public async Task BackTest_CostsReduceResult(string filePath)
    {
        var withoutCosts = await RunFileAsync(filePath, CreateStrategy(), new BacktestExecutionOptions());
        var withCosts = await RunFileAsync(
            filePath,
            CreateStrategy(),
            new BacktestExecutionOptions
            {
                SlippagePercent = 0.05,
                CommissionPercent = 0.05,
            });

        PrintResult(filePath, "no-costs", withoutCosts);
        PrintResult(filePath, "with-costs", withCosts);

        Assert.Multiple(() =>
        {
            AssertCompleted(withoutCosts);
            AssertCompleted(withCosts);
            Assert.That(withCosts.Cash, Is.LessThanOrEqualTo(withoutCosts.Cash),
                "Проскальзывание и комиссия не могут улучшить финансовый результат");
            Assert.That(withCosts.Commission, Is.GreaterThanOrEqualTo(withoutCosts.Commission));
        });
    }

    [Test]
    public async Task BackTest_ClosesOpenPositionsAtLastKnownBook()
    {
        // Arrange: длинная серия плоских цен, затем выброс по Y, после
        // которого данные заканчиваются. Позиция остаётся открытой.
        var strategy = CreateStrategy(entryThreshold: 1.0, exitThreshold: 0.3, warmupTicks: 0);
        var start = new DateTime(2026, 10, 6, 10, 0, 0, DateTimeKind.Utc);
        var books = new List<OrderBookDepth>();

        for (var i = 0; i < 200; i++)
        {
            var timestamp = start.AddSeconds(i);
            books.Add(Book(timestamp, AssetX, bid: 99, ask: 101));
            books.Add(Book(timestamp, AssetY, bid: 149, ask: 151));
        }

        var spikeTime = start.AddSeconds(200);
        books.Add(Book(spikeTime, AssetX, bid: 99, ask: 101));
        books.Add(Book(spikeTime, AssetY, bid: 155, ask: 157));

        // Файл может заканчиваться пустым снимком: ликвидация должна
        // использовать последний пригодный стакан.
        var emptyTime = spikeTime.AddMilliseconds(1);
        books.Add(new OrderBookDepth(emptyTime, AssetX, [], []));
        books.Add(new OrderBookDepth(emptyTime, AssetY, [], []));

        var options = new BacktestExecutionOptions
        {
            SlippagePercent = 1.0,
            CommissionPercent = 0.1,
        };

        // Act
        var result = await RunAsync(strategy, AsAsync(books), options);

        PrintResult("synthetic", "liquidation", result);

        var liquidationTrades = result.Trades
            .Skip(result.Trades.Count - result.LiquidationTradeCount)
            .ToArray();

        // Assert: вход состоялся, а накопленные позиции закрыты последним стаканом.
        Assert.Multiple(() =>
        {
            Assert.That(result.SignalCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(result.TradeCount, Is.GreaterThan(result.LiquidationTradeCount));
            Assert.That(result.LiquidationTradeCount, Is.EqualTo(2));
            Assert.That(result.OpenQuantityX, Is.Zero);
            Assert.That(result.OpenQuantityY, Is.Zero);
            Assert.That(strategy.HasOpenPosition, Is.False,
                "После ликвидации стратегия не должна считать позицию открытой");

            // Long X закрывается продажей по bid 99 с проскальзыванием 1%.
            Assert.That(
                liquidationTrades.Single(t => t.AssetId == AssetX).Price,
                Is.EqualTo(99m * 0.99m));

            // Short Y закрывается покупкой по ask 157 с проскальзыванием 1%.
            Assert.That(
                liquidationTrades.Single(t => t.AssetId == AssetY).Price,
                Is.EqualTo(157m * 1.01m));
        });
    }

    private static async Task<BacktestResult> RunFileAsync(
        string filePath,
        KalmanFilterBasicStrategy strategy,
        BacktestExecutionOptions options)
        => await RunAsync(strategy, OrderBookFileReader.ReadAsync(filePath), options);

    private static async Task<BacktestResult> RunAsync(
        KalmanFilterBasicStrategy strategy,
        IAsyncEnumerable<OrderBookDepth> books,
        BacktestExecutionOptions options)
    {
        var execution = new BacktestOrderExecution(options);
        var portfolio = new Portfolio { StrategyId = strategy.StrategyId };
        var trades = new List<MyTrade>();

        // Для ликвидации в конце берём последний пригодный стакан: файл может
        // заканчиваться пустым или односторонним снимком.
        OrderBookDepth? lastValidBookX = null;
        OrderBookDepth? lastValidBookY = null;
        var signalCount = 0;

        await foreach (var book in books)
        {
            if (book.AssetId == AssetX && IsUsable(book))
            {
                lastValidBookX = book;
            }
            else if (book.AssetId == AssetY && IsUsable(book))
            {
                lastValidBookY = book;
            }

            var signal = strategy.ProcessBook(book);

            if (signal is null)
            {
                continue;
            }

            signalCount++;
            Apply(portfolio, execution.Execute(signal), trades);
            ReconcilePosition(strategy, portfolio);
        }

        // Закрываем всё, что осталось открытым, по последнему известному
        // стакану. Для ликвидации используем те же издержки, но без
        // ограничений частичного исполнения: цель — гарантированно выйти
        // из позиции, а не смоделировать ещё одну порцию ликвидности.
        var liquidationExecution = new BacktestOrderExecution(new BacktestExecutionOptions
        {
            SlippagePercent = options.SlippagePercent,
            CommissionPercent = options.CommissionPercent,
            MaxTradeLots = options.MaxTradeLots,
        });

        var liquidationTrades = CloseOpenPositions(
            strategy,
            portfolio,
            liquidationExecution,
            lastValidBookX,
            lastValidBookY);

        Apply(portfolio, liquidationTrades, trades);
        ReconcilePosition(strategy, portfolio);

        var positionX = portfolio.Get(AssetX)?.Quantity ?? 0m;
        var positionY = portfolio.Get(AssetY)?.Quantity ?? 0m;

        return new BacktestResult
        {
            SignalCount = signalCount,
            Trades = trades,
            LiquidationTradeCount = liquidationTrades.Length,
            OpenQuantityX = positionX,
            OpenQuantityY = positionY,
            Cash = portfolio.Positions.Sum(p => p.Cash),
            Commission = portfolio.Commission,
        };
    }

    private static MyTrade[] CloseOpenPositions(
        KalmanFilterBasicStrategy strategy,
        Portfolio portfolio,
        BacktestOrderExecution execution,
        OrderBookDepth? lastBookX,
        OrderBookDepth? lastBookY)
    {
        var xQtyLots = -ToLots(portfolio.Get(AssetX)?.Quantity ?? 0m);
        var yQtyLots = -ToLots(portfolio.Get(AssetY)?.Quantity ?? 0m);

        if (xQtyLots == 0 && yQtyLots == 0)
        {
            return [];
        }

        if (lastBookX is null || lastBookY is null)
        {
            throw new InvalidOperationException(
                "Cannot close open position: no last known order book for the pair.");
        }

        var liquidation = new PairTradingSignal
        {
            StrategyId = strategy.StrategyId,
            // Для закрытия нужен полный объём: расширяем последний стакан,
            // чтобы не получить частичное исполнение из-за его исходной глубины.
            BookX = MarketBookForLiquidation(lastBookX, xQtyLots),
            BookY = MarketBookForLiquidation(lastBookY, yQtyLots),
            XQtyLots = xQtyLots,
            YQtyLots = yQtyLots,
        };

        return execution.Execute(liquidation);
    }

    private static OrderBookDepth MarketBookForLiquidation(OrderBookDepth book, int requiredLots)
    {
        var size = (double)Math.Max(1m, Math.Abs((decimal)requiredLots));
        var bid = book.BestBid;
        var ask = book.BestAsk;

        if (bid is not > 0 || ask is not > 0)
        {
            throw new InvalidOperationException(
                $"Cannot close position for asset {book.AssetId}: last book has no prices.");
        }

        return new OrderBookDepth(
            book.Timestamp,
            book.AssetId,
            [new OrderBookLevel(bid.Value, size)],
            [new OrderBookLevel(ask.Value, size)]);
    }

    private static bool IsUsable(OrderBookDepth book)
        => book.IsValid && book.BestBid is > 0 && book.BestAsk is > 0;

    private static void Apply(Portfolio portfolio, IEnumerable<MyTrade> trades, List<MyTrade> allTrades)
    {
        foreach (var trade in trades)
        {
            portfolio.Update(trade);
            allTrades.Add(trade);
        }
    }

    private static void ReconcilePosition(KalmanFilterBasicStrategy strategy, Portfolio portfolio)
    {
        strategy.ReconcilePosition(
            ToLots(portfolio.Get(AssetX)?.Quantity ?? 0m),
            ToLots(portfolio.Get(AssetY)?.Quantity ?? 0m));
    }

    private static int ToLots(decimal quantity)
        => decimal.ToInt32(decimal.Round(quantity, 0, MidpointRounding.ToZero));

    private static void AssertCompleted(BacktestResult result)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.OpenQuantityX, Is.Zero, "Открытая позиция X должна быть закрыта в конце бэктеста");
            Assert.That(result.OpenQuantityY, Is.Zero, "Открытая позиция Y должна быть закрыта в конце бэктеста");
            Assert.That(result.Commission, Is.GreaterThanOrEqualTo(0m));
            Assert.That(result.Trades.All(t => t.Price > 0m), Is.True,
                "Все сделки должны иметь положительную цену");
            Assert.That(result.Trades.All(t => t.Commission >= 0m), Is.True,
                "Комиссия по сделке не может быть отрицательной");
        });
    }

    private static void PrintResult(string filePath, string scenario, BacktestResult result)
    {
        var fileName = Path.GetFileName(filePath);

        TestContext.Out.WriteLine(
            $"{fileName} [{scenario}]: signals={result.SignalCount}, trades={result.TradeCount}, " +
            $"liquidation={result.LiquidationTradeCount}, cash={result.Cash:0.####}, " +
            $"commission={result.Commission:0.####}, " +
            $"openX={result.OpenQuantityX}, openY={result.OpenQuantityY}");
    }

    private static KalmanFilterBasicStrategy CreateStrategy(
        double entryThreshold = 2.0,
        double exitThreshold = 0.5,
        int maxLots = 10,
        int warmupTicks = 50,
        double feeRateX = 0.0,
        double feeRateY = 0.0)
    {
        var metadata = new AlgoMetadata
        {
            AssetX = AssetX,
            AssetY = AssetY,
            StrategyId = Guid.NewGuid(),
            AlgoType = nameof(KalmanFilterBasicStrategy),
            AlgoParams = new Dictionary<string, double>
            {
                [AlgoMetadata.Keys.EntryThreshold] = entryThreshold,
                [AlgoMetadata.Keys.ExitThreshold] = exitThreshold,
                [AlgoMetadata.Keys.MaxLots] = maxLots,
                [AlgoMetadata.Keys.WarmupTicks] = warmupTicks,
                [AlgoMetadata.Keys.FeeRateX] = feeRateX,
                [AlgoMetadata.Keys.FeeRateY] = feeRateY,
            },
        };

        return new KalmanFilterBasicStrategy(metadata);
    }

    private static OrderBookDepth Book(DateTime timestamp, ushort assetId, double bid, double ask, double size = 100.0)
        => new(
            timestamp,
            assetId,
            [new OrderBookLevel(bid, size)],
            [new OrderBookLevel(ask, size)]);

    private static async IAsyncEnumerable<OrderBookDepth> AsAsync(IEnumerable<OrderBookDepth> books)
    {
        foreach (var book in books)
        {
            yield return book;
        }

        await Task.CompletedTask;
    }

    private sealed record BacktestResult
    {
        public required int SignalCount { get; init; }

        public required IReadOnlyList<MyTrade> Trades { get; init; }

        public required int LiquidationTradeCount { get; init; }

        public required decimal OpenQuantityX { get; init; }

        public required decimal OpenQuantityY { get; init; }

        public required decimal Cash { get; init; }

        public required decimal Commission { get; init; }

        public int TradeCount => Trades.Count;
    }
}
