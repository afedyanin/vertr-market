using Market.Core.Algos;
using Market.Core.Algos.Common;
using Market.Core.Algos.PairTradingV2;
using Market.Core.Models;
using Market.Core.Tests.Stubs;

namespace Market.Core.Tests.Algos.PairTradingV2;

/// <summary>
/// Тесты стратегии <see cref="KalmanFilterBasicStrategy"/> на синтетических данных.
///
/// Стратегия обновляет фильтр только по событию Y (модель
/// <c>PriceY = Beta * PriceX + Alpha</c>), поэтому X-книга сама по себе
/// не должна приводить к сигналу. Пороги входа/выхода в тестах подбираются
/// под сценарий, чтобы проверить логику входов, выходов и защит.
/// </summary>
[TestFixture]
public class KalmanFilterBasicStrategyTests
{
    private const ushort AssetX = 1;
    private const ushort AssetY = 5;
    private static readonly DateTime StartTime = new(2026, 10, 6, 10, 0, 0, DateTimeKind.Utc);

    private sealed record SignalTrace(int Tick, PairTradingSignal Signal);

    [Test]
    public void ProcessBook_WithOnlyOneLeg_DoesNotEmitSignal()
    {
        // Arrange
        var strategy = CreateStrategy();
        var signals = new List<PairTradingSignal>();

        // Act: кормим только X
        for (var i = 0; i < 50; i++)
        {
            var book = CreateBook(StartTime.AddSeconds(i), AssetX, 100 + i, 100 + i);
            var signal = strategy.ProcessBook(book);
            if (signal is not null)
            {
                signals.Add(signal);
            }
        }

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(signals, Is.Empty);
            Assert.That(strategy.HasOpenPosition, Is.False);
        });
    }

    [Test]
    public void ProcessBook_InvalidOrForeignBook_IsIgnored()
    {
        // Arrange
        var strategy = CreateStrategy();
        var timestamp = StartTime;
        var emptyBook = new OrderBookDepth(timestamp, AssetX, [], []);
        var oneSidedBook = new OrderBookDepth(timestamp, AssetX, [new OrderBookLevel(99.0, 1)], []);
        var foreignBook = CreateBook(timestamp, 999, 100.0, 100.0);
        var yBook = CreateBook(timestamp, AssetY, 150.0, 150.0);

        // Act & Assert: невалидные/чужие стаканы не должны создавать сигнал
        Assert.Multiple(() =>
        {
            Assert.That(strategy.ProcessBook(emptyBook), Is.Null);
            Assert.That(strategy.ProcessBook(oneSidedBook), Is.Null);
            Assert.That(strategy.ProcessBook(foreignBook), Is.Null);
        });

        // После невалидного X валидный Y всё ещё не образует пару
        Assert.That(strategy.ProcessBook(yBook), Is.Null);
        Assert.That(strategy.HasOpenPosition, Is.False);
    }

    [Test]
    public void AlgoFactory_CreatesStrategyByName()
    {
        // Arrange
        var metadata = new AlgoMetadata
        {
            AssetX = AssetX,
            AssetY = AssetY,
            StrategyId = Guid.NewGuid(),
            AlgoType = nameof(KalmanFilterBasicStrategy),
        };

        // Act
        var algo = AlgoFactory.Create(metadata);

        // Assert
        Assert.That(algo, Is.InstanceOf<KalmanFilterBasicStrategy>());
    }

    [Test]
    public void InvalidConfiguration_FailsFast()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateStrategy(entryThreshold: 1.0, exitThreshold: 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateStrategy(lotSizeX: 0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateStrategy(lotSizeY: 0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateStrategy(maxLots: 0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateStrategy(delta: 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateStrategy(delta: 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateStrategy(obsNoise: 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateStrategy(priceRatio: 0.0));
        });
    }

    [Test]
    public void WarmupTicks_PreventsTradingBeforeConfiguredNumber()
    {
        // Arrange
        const int warmupTicks = 100;
        var data = SyntheticMarketDataGenerator.GenerateStationaryPair(
            steps: 800,
            trueBeta: 1.5,
            trueAlpha: 10.0,
            noiseStdDev: 1.0);

        var strategy = CreateStrategy(
            entryThreshold: 0.5,
            exitThreshold: 0.1,
            warmupTicks: warmupTicks);

        // Act
        var traces = Run(strategy, ToPrices(data));

        // Assert
        Assert.That(traces, Is.Not.Empty);
        Assert.That(traces.All(t => t.Tick >= warmupTicks), Is.True,
            $"Обнаружен сигнал до окончания warm-up: tick={traces[0].Tick}");
    }

    [Test]
    public void DuplicateYBook_IsIgnoredAndDoesNotClosePositionImmediately()
    {
        // Arrange: постоянная серия, затем выброс в Y
        var strategy = CreateStrategy(entryThreshold: 1.0, exitThreshold: 0.3, warmupTicks: 0);
        var time = StartTime;

        for (var i = 0; i < 200; i++)
        {
            _ = strategy.ProcessBook(CreateBook(time, AssetX, 100.0, 100.0));
            _ = strategy.ProcessBook(CreateBook(time, AssetY, 150.0, 150.0));
            time = time.AddSeconds(1);
        }

        var bookX = CreateBook(time, AssetX, 100.0, 100.0);
        var bookY = CreateBook(time, AssetY, 156.0, 156.0);

        // Act
        Assert.That(strategy.ProcessBook(bookX), Is.Null);
        var entry = strategy.ProcessBook(bookY);

        // Assert: вход произошёл, а повторная Y-книга с тем же timestamp
        // не обновляет фильтр и не закрывает позицию тем же событием.
        Assert.That(entry, Is.Not.Null);
        Assert.That(strategy.HasOpenPosition, Is.True);
        Assert.That(strategy.ProcessBook(bookY), Is.Null);
        Assert.That(strategy.HasOpenPosition, Is.True);
    }

    [Test]
    public void TimeStop_ClosesPositionAfterMaxHolding()
    {
        // Arrange
        const int spikeStep = 200;
        const double maxHoldingSeconds = 5.0;

        var prices = new List<(double X, double Y)>();
        for (var i = 0; i < 400; i++)
        {
            prices.Add((100.0, i == spikeStep ? 156.0 : 150.0));
        }

        var strategy = CreateStrategy(
            entryThreshold: 1.0,
            exitThreshold: 0.0,
            warmupTicks: 0,
            maxHoldingSeconds: maxHoldingSeconds);

        // Act
        var traces = Run(strategy, prices);

        // Assert
        Assert.That(traces, Has.Count.EqualTo(2));
        var entry = traces[0].Signal;
        var exit = traces[1].Signal;

        Assert.Multiple(() =>
        {
            Assert.That(traces[0].Tick, Is.EqualTo(spikeStep));
            Assert.That(traces[1].Tick, Is.EqualTo(spikeStep + (int)maxHoldingSeconds));
            Assert.That(exit.XQtyLots, Is.EqualTo(-entry.XQtyLots));
            Assert.That(exit.YQtyLots, Is.EqualTo(-entry.YQtyLots));
            Assert.That(strategy.HasOpenPosition, Is.False);
        });
    }

    [Test]
    public void ReconcilePosition_IsUsedForExitQuantities()
    {
        // Arrange
        const int spikeStep = 200;
        const double maxHoldingSeconds = 5.0;
        var strategy = CreateStrategy(
            entryThreshold: 1.0,
            exitThreshold: 0.0,
            warmupTicks: 0,
            maxHoldingSeconds: maxHoldingSeconds);

        var time = StartTime;
        for (var i = 0; i < spikeStep; i++)
        {
            _ = strategy.ProcessBook(CreateBook(time, AssetX, 100.0, 100.0));
            _ = strategy.ProcessBook(CreateBook(time, AssetY, 150.0, 150.0));
            time = time.AddSeconds(1);
        }

        _ = strategy.ProcessBook(CreateBook(time, AssetX, 100.0, 100.0));
        var entry = strategy.ProcessBook(CreateBook(time, AssetY, 156.0, 156.0));
        Assert.That(entry, Is.Not.Null);

        // Фактически исполнилось меньше, чем было в сигнале
        strategy.ReconcilePosition(xQtyLots: 3, yQtyLots: -2);

        // Act: ждём срабатывания time stop
        PairTradingSignal? exit = null;
        for (var i = 1; i <= (int)maxHoldingSeconds; i++)
        {
            time = time.AddSeconds(1);
            _ = strategy.ProcessBook(CreateBook(time, AssetX, 100.0, 100.0));
            exit = strategy.ProcessBook(CreateBook(time, AssetY, 150.0, 150.0));
        }

        // Assert
        Assert.That(exit, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(exit!.XQtyLots, Is.EqualTo(-3));
            Assert.That(exit.YQtyLots, Is.EqualTo(2));
            Assert.That(strategy.HasOpenPosition, Is.False);
        });
    }

    [Test]
    public void FeeGuard_BlocksEntryWhenProfitDoesNotCoverFees()
    {
        // Arrange
        var prices = new List<(double X, double Y)>();
        for (var i = 0; i < 400; i++)
        {
            prices.Add((100.0, i == 200 ? 156.0 : 150.0));
        }

        var withoutFees = Run(
            CreateStrategy(entryThreshold: 1.0, exitThreshold: 0.3, warmupTicks: 0),
            prices);

        var withFees = Run(
            CreateStrategy(
                entryThreshold: 1.0,
                exitThreshold: 0.3,
                warmupTicks: 0,
                feeRateX: 0.05,
                feeRateY: 0.05),
            prices);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(withoutFees, Is.Not.Empty);
            Assert.That(withFees, Is.Empty);
        });
    }

    [Test]
    public void InvalidMidPrice_IsIgnoredWithoutPoisoningFilter()
    {
        // Arrange
        var strategy = CreateStrategy(entryThreshold: 1.0, exitThreshold: 0.3, warmupTicks: 0);
        var time = StartTime;

        for (var i = 0; i < 100; i++)
        {
            _ = strategy.ProcessBook(CreateBook(time, AssetX, 100.0, 100.0));
            _ = strategy.ProcessBook(CreateBook(time, AssetY, 150.0, 150.0));
            time = time.AddSeconds(1);
        }

        var nanBookY = new OrderBookDepth(
            time,
            AssetY,
            [new OrderBookLevel(double.NaN, 1)],
            [new OrderBookLevel(double.NaN, 1)]);

        // Act
        var nanSignal = strategy.ProcessBook(nanBookY);
        time = time.AddSeconds(1);

        var bookX = CreateBook(time, AssetX, 100.0, 100.0);
        var bookY = CreateBook(time, AssetY, 156.0, 156.0);
        _ = strategy.ProcessBook(bookX);
        var spikeSignal = strategy.ProcessBook(bookY);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(nanSignal, Is.Null);
            Assert.That(spikeSignal, Is.Not.Null, "NaN-цена не должна ломать состояние фильтра");
        });
    }

    [Test]
    public void StationaryRegime_WhenZScoreIsBelowThreshold_DoesNotEnter()
    {
        // Arrange
        var data = SyntheticMarketDataGenerator.GenerateStationaryPair(
            steps: 500,
            trueBeta: 1.5,
            trueAlpha: 10.0,
            noiseStdDev: 1.0);

        var strategy = CreateStrategy(entryThreshold: 10.0, exitThreshold: 0.01);

        // Act
        var traces = Run(strategy, ToPrices(data));

        // Assert: порог 10 сигма недостижим на коинтегрированной паре
        Assert.Multiple(() =>
        {
            Assert.That(traces, Is.Empty);
            Assert.That(strategy.HasOpenPosition, Is.False);
        });
    }

    [Test]
    public void StationaryRegime_EntriesRespectThresholdDirectionAndLotBounds()
    {
        // Arrange
        const double entryThreshold = 0.5;
        const double exitThreshold = 0.1;
        const int maxLots = 5;

        var data = SyntheticMarketDataGenerator.GenerateStationaryPair(
            steps: 800,
            trueBeta: 1.5,
            trueAlpha: 10.0,
            noiseStdDev: 1.0);

        var strategy = CreateStrategy(entryThreshold, exitThreshold, maxLots);

        // Act
        var traces = Run(strategy, ToPrices(data));

        // Assert
        Assert.That(traces, Is.Not.Empty, "На коинтегрированной паре с низким порогом ожидались сделки");
        var (openX, openY) = AssertSignalInvariants(traces, entryThreshold, exitThreshold, maxLots);
        Assert.That(strategy.HasOpenPosition, Is.EqualTo(openX != 0 || openY != 0));
    }

    [Test]
    public void OpenPosition_IsNotDoubledWhileExitThresholdIsNotReached()
    {
        // Arrange: exit = 1e-9 делает выход практически недостижимым
        const double entryThreshold = 0.8;
        const double exitThreshold = 1e-9;
        const int maxLots = 5;

        var data = SyntheticMarketDataGenerator.GenerateStationaryPair(
            steps: 800,
            trueBeta: 1.5,
            trueAlpha: 10.0,
            noiseStdDev: 1.0);

        var strategy = CreateStrategy(entryThreshold, exitThreshold, maxLots);

        // Act
        var traces = Run(strategy, ToPrices(data));

        // Assert: пока позиция открыта, повторных входов быть не может
        Assert.That(traces, Has.Count.EqualTo(1), "Ожидался ровно один вход без усреднения позиции");
        var entry = traces[0].Signal;
        Assert.Multiple(() =>
        {
            Assert.That(Math.Abs(entry.Params[SignalParamKeys.ZScore]), Is.GreaterThan(entryThreshold));
            Assert.That(entry.XQtyLots, Is.Not.Zero);
            Assert.That(entry.YQtyLots, Is.Not.Zero);
            Assert.That(strategy.HasOpenPosition, Is.True);
        });
    }

    [Test]
    public void OutlierSpike_OpensAndThenClosesPositionWithOppositeLots()
    {
        // Arrange: детерминированная синтетическая серия без шума,
        // чтобы вход и выход происходили строго на выбросе.
        const int spikeStep = 200;
        const double entryThreshold = 1.0;
        const double exitThreshold = 0.3;

        var prices = new List<(double X, double Y)>();
        for (var i = 0; i < 400; i++)
        {
            prices.Add((100.0, i == spikeStep ? 156.0 : 150.0));
        }

        var strategy = CreateStrategy(entryThreshold, exitThreshold);

        // Act
        var traces = Run(strategy, prices);

        // Assert
        Assert.That(traces, Has.Count.EqualTo(2), "Ожидались ровно вход по выбросу и последующий выход");
        var entry = traces[0].Signal;
        var exit = traces[1].Signal;

        Assert.Multiple(() =>
        {
            Assert.That(traces[0].Tick, Is.EqualTo(spikeStep));
            Assert.That(entry.Params[SignalParamKeys.ZScore], Is.GreaterThan(entryThreshold));
            Assert.That(entry.XQtyLots, Is.GreaterThan(0), "Y дорог — ожидался Long X");
            Assert.That(entry.YQtyLots, Is.LessThan(0), "Y дорог — ожидался Short Y");

            Assert.That(Math.Abs(exit.Params[SignalParamKeys.ZScore]), Is.LessThan(exitThreshold));
            Assert.That(exit.XQtyLots, Is.EqualTo(-entry.XQtyLots));
            Assert.That(exit.YQtyLots, Is.EqualTo(-entry.YQtyLots));

            Assert.That(strategy.HasOpenPosition, Is.False);
        });
    }

    [Test]
    public void RegimeShift_KeepsProducingValidSignalsInBothRegimes()
    {
        // Arrange
        const int shiftStep = 500;
        const double entryThreshold = 1.0;
        const double exitThreshold = 0.3;
        const int maxLots = 5;

        var data = SyntheticMarketDataGenerator.GenerateRegimeShiftPair(
            steps: 1200,
            shiftStep: shiftStep,
            beta1: 1.2, alpha1: 5.0,
            beta2: 2.0, alpha2: -15.0,
            noiseStdDev: 1.0);

        var strategy = CreateStrategy(entryThreshold, exitThreshold, maxLots);

        // Act
        var traces = Run(strategy, ToPrices(data));

        // Assert
        Assert.That(traces, Is.Not.Empty);
        Assert.That(traces.Any(t => t.Tick < shiftStep), Is.True,
            "До смены режима не было ни одной сделки");
        Assert.That(traces.Any(t => t.Tick >= shiftStep), Is.True,
            "После смены режима стратегия перестала торговать");

        _ = AssertSignalInvariants(traces, entryThreshold, exitThreshold, maxLots);
    }

    [Test]
    public void DriftingBeta_ProducesBoundedFiniteSignals()
    {
        // Arrange
        const double entryThreshold = 1.0;
        const double exitThreshold = 0.3;
        const int maxLots = 5;

        var data = SyntheticMarketDataGenerator.GenerateDriftingBetaPair(
            steps: 1000,
            startBeta: 1.0,
            endBeta: 2.5,
            trueAlpha: 5.0,
            noiseStdDev: 0.8);

        var strategy = CreateStrategy(entryThreshold, exitThreshold, maxLots);

        // Act
        var traces = Run(strategy, ToPrices(data));

        // Assert: дрейф beta не должен ломать численную устойчивость и размеры лотов
        Assert.That(traces, Is.Not.Empty);
        _ = AssertSignalInvariants(traces, entryThreshold, exitThreshold, maxLots);
    }

    [Test]
    public void DivergingPair_KeepsSignalsWellFormed()
    {
        // Arrange
        const int divergeStep = 100;
        const double entryThreshold = 0.5;
        const double exitThreshold = 0.1;
        const int maxLots = 10;

        var data = SyntheticMarketDataGenerator.GenerateDivergingPair(
            steps: 400,
            divergeStep: divergeStep,
            trueBeta: 1.5,
            trueAlpha: 0.0);

        var strategy = CreateStrategy(entryThreshold, exitThreshold, maxLots);

        // Act
        var traces = Run(strategy, ToPrices(data));

        // Assert: даже при разрушении коинтеграции сигналы остаются корректными
        Assert.That(traces, Is.Not.Empty);
        Assert.That(traces.Any(t => t.Tick >= divergeStep), Is.True,
            "После разрушения коинтеграции стратегия перестала генерировать сигналы");
        _ = AssertSignalInvariants(traces, entryThreshold, exitThreshold, maxLots);
    }

    [Test]
    public void PriceRatio_ScalesFilteredPriceXButNotTheOrderBook()
    {
        // Arrange
        const double priceRatio = 2.0;
        var strategy = CreateStrategy(entryThreshold: 2.0, exitThreshold: 0.5, priceRatio: priceRatio);

        var prices = new List<(double X, double Y)>();
        for (var i = 0; i < 120; i++)
        {
            prices.Add((100.0, 200.0));
        }

        // Резкий сдвиг X: в масштабе фильтра это 300 против Y = 200
        prices.Add((150.0, 200.0));

        // Act
        var traces = Run(strategy, prices);

        // Assert
        Assert.That(traces, Is.Not.Empty, "Сдвиг X должен был дать вход");
        var signal = traces[0].Signal;
        Assert.Multiple(() =>
        {
            Assert.That(signal.Params[SignalParamKeys.PriceX], Is.EqualTo(150.0 * priceRatio).Within(1e-9));
            Assert.That(signal.Params[SignalParamKeys.PriceY], Is.EqualTo(200.0).Within(1e-9));
            Assert.That(signal.BookX.MidPrice, Is.EqualTo(150.0).Within(1e-9));
            Assert.That(signal.BookY.MidPrice, Is.EqualTo(200.0).Within(1e-9));
        });
    }

    [Test]
    public void LongRunStationaryData_DoesNotProduceNaNAndKeepsSizesBounded()
    {
        // Arrange
        const double entryThreshold = 0.5;
        const double exitThreshold = 0.1;
        const int maxLots = 10;

        var data = SyntheticMarketDataGenerator.GenerateStationaryPair(
            steps: 3000,
            trueBeta: 1.5,
            trueAlpha: 10.0,
            noiseStdDev: 1.0);

        var strategy = CreateStrategy(entryThreshold, exitThreshold, maxLots);

        // Act
        var traces = Run(strategy, ToPrices(data));

        // Assert
        Assert.That(traces, Is.Not.Empty);
        _ = AssertSignalInvariants(traces, entryThreshold, exitThreshold, maxLots);
    }

    private static KalmanFilterBasicStrategy CreateStrategy(
        double entryThreshold = 2.0,
        double exitThreshold = 0.5,
        int maxLots = 10,
        int lotSizeX = 1,
        int lotSizeY = 1,
        double priceRatio = 1.0,
        double delta = 1e-4,
        double obsNoise = 1.0,
        int warmupTicks = 50,
        double maxHoldingSeconds = 0.0,
        double maxAbsZScore = 0.0,
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
                [AlgoMetadata.Keys.LotSizeX] = lotSizeX,
                [AlgoMetadata.Keys.LotSizeY] = lotSizeY,
                [AlgoMetadata.Keys.PriceRatio] = priceRatio,
                [AlgoMetadata.Keys.Delta] = delta,
                [AlgoMetadata.Keys.ObsNoise] = obsNoise,
                [AlgoMetadata.Keys.WarmupTicks] = warmupTicks,
                [AlgoMetadata.Keys.MaxHoldingSeconds] = maxHoldingSeconds,
                [AlgoMetadata.Keys.MaxAbsZScore] = maxAbsZScore,
                [AlgoMetadata.Keys.FeeRateX] = feeRateX,
                [AlgoMetadata.Keys.FeeRateY] = feeRateY,
            },
        };

        return new KalmanFilterBasicStrategy(metadata);
    }

    private static List<(double X, double Y)> ToPrices(
        IEnumerable<SyntheticMarketDataGenerator.PriceTick> data)
        => [.. data.Select(tick => (tick.X, tick.Y))];

    private static List<SignalTrace> Run(
        KalmanFilterBasicStrategy strategy,
        IEnumerable<(double X, double Y)> prices)
    {
        var traces = new List<SignalTrace>();
        var tick = 0;

        foreach (var (priceX, priceY) in prices)
        {
            var timestamp = StartTime.AddSeconds(tick);
            var bookX = CreateBook(timestamp, AssetX, priceX, priceX);
            var bookY = CreateBook(timestamp, AssetY, priceY, priceY);

            // Стратегия обрабатывает оба стакана независимо, поэтому одна пара
            // может дать два обновления фильтра и два сигнала.
            var signalX = strategy.ProcessBook(bookX);
            if (signalX is not null)
            {
                traces.Add(new SignalTrace(tick, signalX));
            }

            var signalY = strategy.ProcessBook(bookY);
            if (signalY is not null)
            {
                traces.Add(new SignalTrace(tick, signalY));
            }

            tick++;
        }

        return traces;
    }

    private static OrderBookDepth CreateBook(DateTime timestamp, ushort assetId, double bid, double ask)
        => OrderBookStub.Create(timestamp, assetId, bid, ask);

    /// <summary>
    /// Проверяет инварианты последовательности сигналов и возвращает накопленную
    /// открытую позицию. Направление входа зафиксировано текущей реализацией:
    /// ZScore &gt; 0 =&gt; Long X / Short Y, ZScore &lt; 0 =&gt; Short X / Long Y.
    /// </summary>
    private static (int OpenX, int OpenY) AssertSignalInvariants(
        IReadOnlyList<SignalTrace> traces,
        double entryThreshold,
        double exitThreshold,
        int maxLots)
    {
        var openX = 0;
        var openY = 0;

        foreach (var trace in traces)
        {
            var signal = trace.Signal;
            var zScore = signal.Params[SignalParamKeys.ZScore];
            var beta = signal.Params[SignalParamKeys.Beta];

            Assert.Multiple(() =>
            {
                Assert.That(double.IsFinite(zScore), Is.True, $"Tick {trace.Tick}: ZScore={zScore}");
                Assert.That(double.IsFinite(beta), Is.True, $"Tick {trace.Tick}: Beta={beta}");
                Assert.That(Math.Abs(signal.XQtyLots), Is.LessThanOrEqualTo(maxLots),
                    $"Tick {trace.Tick}: XQtyLots={signal.XQtyLots}");
                Assert.That(Math.Abs(signal.YQtyLots), Is.LessThanOrEqualTo(maxLots),
                    $"Tick {trace.Tick}: YQtyLots={signal.YQtyLots}");
            });

            if (openX == 0 && openY == 0)
            {
                // Вход: сигнал должен быть за порогом, а направление — по знаку Z
                Assert.Multiple(() =>
                {
                    Assert.That(Math.Abs(zScore), Is.GreaterThan(entryThreshold),
                        $"Tick {trace.Tick}: вход при ZScore={zScore}");
                    Assert.That(signal.XQtyLots, Is.Not.Zero);
                    Assert.That(signal.YQtyLots, Is.Not.Zero);
                    Assert.That(Math.Sign(signal.XQtyLots), Is.EqualTo(Math.Sign(zScore)),
                        $"Tick {trace.Tick}: XQtyLots={signal.XQtyLots}, ZScore={zScore}");
                    Assert.That(Math.Sign(signal.YQtyLots), Is.EqualTo(-Math.Sign(zScore)),
                        $"Tick {trace.Tick}: YQtyLots={signal.YQtyLots}, ZScore={zScore}");
                });
            }
            else
            {
                // Выход: только при возврате Z в коридор и ровно противоположным объёмом
                Assert.Multiple(() =>
                {
                    Assert.That(Math.Abs(zScore), Is.LessThan(exitThreshold),
                        $"Tick {trace.Tick}: выход при ZScore={zScore}, openX={openX}, openY={openY}");
                    Assert.That(signal.XQtyLots, Is.EqualTo(-openX));
                    Assert.That(signal.YQtyLots, Is.EqualTo(-openY));
                });
            }

            openX += signal.XQtyLots;
            openY += signal.YQtyLots;
        }

        return (openX, openY);
    }
}
