using Market.Core.Algos.Common;
using Market.Core.Models;

namespace Market.Core.Algos.PairTradingV2;

/// <summary>
/// Базовая стратегия парного трейдинга на фильтре Калмана.
///
/// Модель: <c>PriceY = Beta * PriceX + Alpha</c>. Фильтр обновляется только
/// по событию Y (зависимая нога), X используется как последняя известная цена.
/// Это исключает двойное обновление фильтра на одну и ту же пару стаканов.
/// </summary>
public class KalmanFilterBasicStrategy : PairTradingAlgoBase
{
    private readonly object _sync = new();
    private readonly PairsKalmanFilter _kalmanFilter;

    private readonly double _entryThreshold;
    private readonly double _exitThreshold;
    private readonly int _lotSizeX;
    private readonly int _lotSizeY;
    private readonly int _maxLots;
    private readonly double _priceRatio;
    private readonly int _warmupTicks;
    private readonly double _maxHoldingSeconds;
    private readonly double _maxAbsZScore;
    private readonly double _feeRateX;
    private readonly double _feeRateY;

    private DateTime _lastProcessedTimestamp = DateTime.MinValue;
    private int _processedTicks;
    private DateTime? _entryTimestamp;

    public KalmanFilterBasicStrategy(AlgoMetadata metadata) : base(metadata)
    {
        var delta = GetDouble(AlgoMetadata.Keys.Delta, 1e-4);
        if (delta <= 0.0 || delta >= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), delta,
                $"{AlgoMetadata.Keys.Delta} must be in the (0, 1) interval.");
        }

        var obsNoise = GetDouble(AlgoMetadata.Keys.ObsNoise, 1.0);
        if (obsNoise <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), obsNoise,
                $"{AlgoMetadata.Keys.ObsNoise} must be positive.");
        }

        _kalmanFilter = new PairsKalmanFilter(delta, obsNoise);

        _lotSizeX = GetInt(AlgoMetadata.Keys.LotSizeX, 1, min: 1);
        _lotSizeY = GetInt(AlgoMetadata.Keys.LotSizeY, 1, min: 1);
        _priceRatio = GetDouble(AlgoMetadata.Keys.PriceRatio, 1.0);
        _maxLots = GetInt(AlgoMetadata.Keys.MaxLots, 10, min: 1);
        _entryThreshold = GetDouble(AlgoMetadata.Keys.EntryThreshold, 2.0);
        _exitThreshold = GetDouble(AlgoMetadata.Keys.ExitThreshold, 0.5);
        _warmupTicks = GetInt(AlgoMetadata.Keys.WarmupTicks, 50, min: 0);
        _maxHoldingSeconds = GetDouble(AlgoMetadata.Keys.MaxHoldingSeconds, 0.0);
        _maxAbsZScore = GetDouble(AlgoMetadata.Keys.MaxAbsZScore, 0.0);
        _feeRateX = GetDouble(AlgoMetadata.Keys.FeeRateX, 0.0);
        _feeRateY = GetDouble(AlgoMetadata.Keys.FeeRateY, 0.0);

        if (_priceRatio <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), _priceRatio,
                $"{AlgoMetadata.Keys.PriceRatio} must be positive.");
        }

        if (_entryThreshold <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), _entryThreshold,
                $"{AlgoMetadata.Keys.EntryThreshold} must be positive.");
        }

        if (_exitThreshold < 0.0 || _exitThreshold >= _entryThreshold)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), _exitThreshold,
                $"{AlgoMetadata.Keys.ExitThreshold} must be non-negative and less than the entry threshold.");
        }

        if (_maxHoldingSeconds < 0.0 || _maxAbsZScore < 0.0 || _feeRateX < 0.0 || _feeRateY < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), "Time stop, Z-stop and fee rates must be non-negative.");
        }
    }

    public override PairTradingSignal? ProcessBook(OrderBookDepth book)
    {
        lock (_sync)
        {
            return base.ProcessBook(book);
        }
    }

    /// <summary>
    /// Сверяет внутреннюю позицию с фактической позицией портфеля.
    /// Вызывайте после исполнения/отклонения заявок, чтобы выход считался
    /// от реального объёма, а не от намерения стратегии.
    /// </summary>
    public void ReconcilePosition(int xQtyLots, int yQtyLots)
    {
        lock (_sync)
        {
            _openXQtyLots = xQtyLots;
            _openYQtyLots = yQtyLots;

            if (!HasOpenPosition)
            {
                _entryTimestamp = null;
            }
        }
    }

    // X только запоминается в базовом классе. Фильтр обновляется по Y,
    // поэтому одна пара стаканов не обрабатывается дважды.
    protected override PairTradingSignal? ProcessTickX() => null;

    protected override PairTradingSignal? ProcessTickY() => ProcessTick();

    private PairTradingSignal? ProcessTick()
    {
        // Пока не получили данные по обоим инструментам — расчет невозможен
        if (_lastDepthX is null || _lastDepthY is null)
        {
            return null;
        }

        // Защита от повторной обработки одного и того же события Y
        if (_lastDepthY.Timestamp <= _lastProcessedTimestamp)
        {
            return null;
        }

        var rawPriceX = _lastDepthX.MidPrice;
        var rawPriceY = _lastDepthY.MidPrice;

        // OrderBookDepth.IsValid проверяет только наличие сторон, но не
        // конечность и положительность цен.
        if (rawPriceX is not > 0 || !double.IsFinite(rawPriceX.Value))
        {
            return null;
        }

        if (rawPriceY is not > 0 || !double.IsFinite(rawPriceY.Value))
        {
            return null;
        }

        var priceX = rawPriceX.Value * _priceRatio;
        var priceY = rawPriceY.Value;

        if (!double.IsFinite(priceX))
        {
            return null;
        }

        _lastProcessedTimestamp = _lastDepthY.Timestamp;

        // Модель: PriceY = Beta * PriceX + Alpha
        var stepResult = _kalmanFilter.Update(priceY, priceX);
        _processedTicks++;

        if (!IsFinite(stepResult))
        {
            // Численная неустойчивость: сбрасываем фильтр и пропускаем тик.
            _kalmanFilter.Reset();
            return null;
        }

        if (_processedTicks <= _warmupTicks)
        {
            return null;
        }

        var timestamp = _lastDepthY.Timestamp > _lastDepthX.Timestamp
            ? _lastDepthY.Timestamp
            : _lastDepthX.Timestamp;

        (var xQty, var yQty) = GetQty(stepResult, rawPriceX.Value, priceY, timestamp);

        if (xQty == 0 && yQty == 0)
        {
            return null;
        }

        var wasFlat = !HasOpenPosition;

        var signal = new PairTradingSignal
        {
            StrategyId = StrategyId,
            BookX = _lastDepthX,
            BookY = _lastDepthY,
            XQtyLots = xQty,
            YQtyLots = yQty,
        };

        signal.Params[SignalParamKeys.PriceX] = priceX;
        signal.Params[SignalParamKeys.PriceY] = priceY;
        signal.Params[SignalParamKeys.Alpha] = stepResult.Alpha;
        signal.Params[SignalParamKeys.Beta] = stepResult.Beta;
        signal.Params[SignalParamKeys.Spread] = stepResult.Spread;
        signal.Params[SignalParamKeys.ZScore] = stepResult.ZScore;

        _openXQtyLots += xQty;
        _openYQtyLots += yQty;

        if (wasFlat && HasOpenPosition)
        {
            _entryTimestamp = timestamp;
        }
        else if (!HasOpenPosition)
        {
            _entryTimestamp = null;
        }

        return signal;
    }

    private (int xQty, int yQty) GetQty(
        PairsKalmanFilter.StepResult stepResult,
        double feePriceX,
        double feePriceY,
        DateTime timestamp)
    {
        var zScore = stepResult.ZScore;
        var absZScore = Math.Abs(zScore);

        if (HasOpenPosition)
        {
            // Не закрываем позицию тем же событием, которым она была открыта.
            var isNewEvent = _entryTimestamp is null || timestamp > _entryTimestamp.Value;
            var normalExit = absZScore < _exitThreshold;
            var timeStop = _maxHoldingSeconds > 0.0
                && _entryTimestamp is not null
                && (timestamp - _entryTimestamp.Value).TotalSeconds >= _maxHoldingSeconds;
            var zStop = _maxAbsZScore > 0.0 && absZScore >= _maxAbsZScore;

            if (isNewEvent && (normalExit || timeStop || zStop))
            {
                // Спред вернулся в норму / сработал стоп — фиксируем позицию
                return (-_openXQtyLots, -_openYQtyLots);
            }

            return (0, 0);
        }

        if (absZScore <= _entryThreshold || stepResult.Beta <= 0.0)
        {
            return (0, 0);
        }

        (var xQty, var yQty) = ApproximateLotCalculator.CalculateLots(
            stepResult.Beta,
            _lotSizeX,
            _lotSizeY,
            _maxLots);

        if (xQty <= 0 || yQty <= 0)
        {
            return (0, 0);
        }

        // Ожидаемая прибыль от возврата спреда должна покрывать комиссии.
        var expectedProfit = Math.Abs(stepResult.Spread) * yQty * _lotSizeY;
        var fees = _feeRateX * xQty * _lotSizeX * feePriceX
            + _feeRateY * yQty * _lotSizeY * feePriceY;

        if (expectedProfit <= fees)
        {
            return (0, 0);
        }

        if (zScore > _entryThreshold)
        {
            // Y дорог относительно X: Long X, Short Y
            return (xQty, -yQty);
        }

        // Y дёшев относительно X: Short X, Long Y
        return (-xQty, yQty);
    }

    private double GetDouble(string key, double defaultValue)
    {
        var value = AlgoParams.GetValueOrDefault(key, defaultValue);

        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(key), value,
                $"Parameter '{key}' must be a finite number.");
        }

        return value;
    }

    private int GetInt(string key, int defaultValue, int min)
    {
        var raw = AlgoParams.GetValueOrDefault(key, defaultValue);

        if (!double.IsFinite(raw) || raw != Math.Truncate(raw)
            || raw < int.MinValue || raw > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(key), raw,
                $"Parameter '{key}' must be an integer.");
        }

        var value = (int)raw;

        if (value < min)
        {
            throw new ArgumentOutOfRangeException(nameof(key), value,
                $"Parameter '{key}' must be >= {min}.");
        }

        return value;
    }

    private static bool IsFinite(PairsKalmanFilter.StepResult result)
        => double.IsFinite(result.Beta)
            && double.IsFinite(result.Alpha)
            && double.IsFinite(result.Spread)
            && double.IsFinite(result.ZScore);
}
