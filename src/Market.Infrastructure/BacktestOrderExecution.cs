using Market.Core.Models;

namespace Market.Infrastructure;

/// <summary>
/// Эмулятор <c>OrderExecutionEngineService</c> для бэктестов.
/// </summary>
/// <remarks>
/// <para>
/// Не использует внешнюю инфраструктуру (BackgroundService, T-Invest API,
/// Channels, Redis). На вход получает <see cref="PairTradingSignal"/>, на
/// выходе отдаёт набор исполненных <see cref="MyTrade"/>. Модель
/// детерминированная: один и тот же сигнал и стакан всегда дают одинаковый
/// результат.
/// </para>
/// <para>
/// Заявка исполняется как рыночная по уровням стакана: покупка идёт по
/// <see cref="OrderBookDepth.Asks"/>, продажа — по
/// <see cref="OrderBookDepth.Bids"/>. Если ликвидности в стакане не хватает,
/// заявка исполняется частично. Крупная заявка может дробиться на несколько
/// трейдов (см. <see cref="BacktestExecutionOptions.MaxTradeLots"/>).
/// </para>
/// <para>
/// Проскальзывание включается в цену сделки, а комиссия — в отдельное поле
/// <see cref="MyTrade.Commission"/>: так <see cref="Portfolio"/> и
/// <see cref="Position"/> учитывают её как отдельный денежный расход, не
/// искажая цену исполнения.
/// </para>
/// </remarks>
public sealed class BacktestOrderExecution
{
    private readonly BacktestExecutionOptions _options;
    private readonly decimal _buyPriceFactor;
    private readonly decimal _sellPriceFactor;
    private readonly decimal _commissionRate;

    /// <summary>Создаёт эмулятор без проскальзывания и комиссии.</summary>
    public BacktestOrderExecution()
        : this(new BacktestExecutionOptions())
    {
    }

    /// <summary>
    /// Создаёт эмулятор с заданными издержками.
    /// </summary>
    /// <param name="slippagePercent">Проскальзывание в процентах (0.1 = 0.1%).</param>
    /// <param name="commissionPercent">Комиссия в процентах от объёма сделки (0.05 = 0.05%).</param>
    public BacktestOrderExecution(double slippagePercent, double commissionPercent)
        : this(new BacktestExecutionOptions
        {
            SlippagePercent = slippagePercent,
            CommissionPercent = commissionPercent,
        })
    {
    }

    /// <summary>
    /// Создаёт эмулятор с полной конфигурацией исполнения.
    /// </summary>
    public BacktestOrderExecution(BacktestExecutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ValidatePercent(options.SlippagePercent, nameof(options.SlippagePercent));
        ValidatePercent(options.CommissionPercent, nameof(options.CommissionPercent));

        if (options.MaxTradeLots < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MaxTradeLots,
                $"{nameof(options.MaxTradeLots)} must be non-negative.");
        }

        if (!double.IsFinite(options.MaxLevelParticipation)
            || options.MaxLevelParticipation <= 0.0
            || options.MaxLevelParticipation > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MaxLevelParticipation,
                $"{nameof(options.MaxLevelParticipation)} must be in the (0, 1] interval.");
        }

        _options = options;

        var slippage = options.SlippagePercent / 100.0;

        _buyPriceFactor = (decimal)(1.0 + slippage);
        _sellPriceFactor = (decimal)(1.0 - slippage);
        _commissionRate = (decimal)(options.CommissionPercent / 100.0);
    }

    /// <summary>
    /// Исполняет торговый сигнал и возвращает список фактических сделок.
    /// </summary>
    /// <remarks>
    /// Каждая нога исполняется независимо по своему стакану. Пустой или
    /// односторонний стакан по нужной стороне означает отсутствие исполнения.
    /// </remarks>
    public MyTrade[] Execute(PairTradingSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

        var trades = new List<MyTrade>();
        FillLeg(signal.StrategyId, signal.XQtyLots, signal.BookX, trades);
        FillLeg(signal.StrategyId, signal.YQtyLots, signal.BookY, trades);

        return [.. trades];
    }

    private void FillLeg(Guid strategyId, int qtyLots, OrderBookDepth book, List<MyTrade> trades)
    {
        if (qtyLots == 0 || book is null)
        {
            return;
        }

        // Положительный объём — покупка (идём по ask), отрицательный — продажа (по bid).
        var direction = Math.Sign(qtyLots);
        var levels = direction > 0 ? book.Asks : book.Bids;

        if (levels is not { Length: > 0 })
        {
            return;
        }

        var remainingLots = Math.Abs((decimal)qtyLots);
        var orderId = BuildOrderId(strategyId, book, direction);
        var tradeIndex = 0;

        foreach (var level in levels)
        {
            if (remainingLots <= 0)
            {
                break;
            }

            if (!IsTradable(level))
            {
                continue;
            }

            // Сделками торгуются целые лоты, поэтому доступный объём округляем вниз.
            var availableLots = decimal.Floor((decimal)level.Size * (decimal)_options.MaxLevelParticipation);

            if (availableLots <= 0)
            {
                continue;
            }

            var fillLots = Math.Min(remainingLots, availableLots);
            var price = (decimal)level.Price * (direction > 0 ? _buyPriceFactor : _sellPriceFactor);

            // Дробим исполнение уровня на несколько трейдов, если задан лимит.
            while (fillLots > 0)
            {
                var chunkLots = _options.MaxTradeLots > 0
                    ? Math.Min(fillLots, _options.MaxTradeLots)
                    : fillLots;
                var signedLots = direction * chunkLots;

                trades.Add(new MyTrade
                {
                    StrategyId = strategyId,
                    OrderId = orderId,
                    TradeId = $"{orderId}-{tradeIndex}",
                    AssetId = book.AssetId,
                    Timestamp = book.Timestamp,
                    Qty = signedLots,
                    Price = price,
                    // Комиссия берётся с фактического объёма по цене исполнения.
                    Commission = Math.Abs(signedLots) * price * _commissionRate,
                });

                fillLots -= chunkLots;
                remainingLots -= chunkLots;
                tradeIndex++;
            }
        }
    }

    private static bool IsTradable(OrderBookLevel level)
        => double.IsFinite(level.Price)
            && level.Price > 0.0
            && double.IsFinite(level.Size)
            && level.Size > 0.0;

    private static string BuildOrderId(Guid strategyId, OrderBookDepth book, int direction)
        => $"bt-{strategyId:N}-{book.Timestamp.Ticks}-{book.AssetId}-{(direction > 0 ? "B" : "S")}";

    private static void ValidatePercent(double value, string paramName)
    {
        if (!double.IsFinite(value) || value < 0.0 || value >= 100.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Percent must be in the [0, 100) interval.");
        }
    }
}

/// <summary>
/// Настройки эмуляции исполнения для <see cref="BacktestOrderExecution"/>.
/// </summary>
public sealed record BacktestExecutionOptions
{
    /// <summary>
    /// Проскальзывание в процентах (0.1 = 0.1%). Применяется к цене каждого
    /// уровня: покупка дорожает, продажа дешевеет.
    /// </summary>
    public double SlippagePercent { get; init; }

    /// <summary>
    /// Комиссия в процентах от объёма сделки (0.05 = 0.05%). Записывается в
    /// <see cref="MyTrade.Commission"/> и уменьшает денежный поток портфеля.
    /// </summary>
    public double CommissionPercent { get; init; }

    /// <summary>
    /// Максимальный объём одного трейда в лотах. <c>0</c> — не дробить
    /// исполнение уровня. Например, <c>1</c> даёт по трейду на каждый лот.
    /// </summary>
    public int MaxTradeLots { get; init; }

    /// <summary>
    /// Доля доступного объёма уровня, которую может забрать заявка, в
    /// интервале <c>(0, 1]</c>. Значение меньше единицы эмулирует частичное
    /// исполнение из-за конкуренции за ликвидность.
    /// </summary>
    public double MaxLevelParticipation { get; init; } = 1.0;
}
