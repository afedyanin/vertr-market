namespace Vertr.Market.Palyground.Tests.Candles;

[TestFixture(Category = "Unit")]
public class CandlesBuilderTests
{
    [Test]
    public void CanAggregateCandles()
    {
        // Создаем агрегатор для разных таймфреймов
        var aggregator = new RealTimeAggregator(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMinutes(1)
        );

        var startTime = DateTime.Parse("2023-10-01 10:00:00");

        // Имитируем поток сделок, приходящих по одной
        var streamOfTrades = new List<Trade>
        {
            new(startTime.AddSeconds(1), 100.1, 10),
            new(startTime.AddSeconds(2), 100.2, 5),
            new(startTime.AddSeconds(4), 100.3, 10), // Должна обновить свечу 5с и 10с
            new(startTime.AddSeconds(6), 100.4, 15), // Закроет свечу 5с (интервал 0-5)
            new(startTime.AddSeconds(7), 100.5, 20),
            new(startTime.AddSeconds(11), 100.6, 30), // Закроет свечу 10с (интервал 0-10)
            new(startTime.AddSeconds(65), 100.7, 10), // Закроет свечу 1мин (интервал 0-60)
        };

        foreach (var trade in streamOfTrades)
        {
            Console.WriteLine($"Trade: {trade.Time:HH:mm:ss} Price:{trade.Price}");
            aggregator.AddTrade(trade);
        }

        Assert.Pass();
    }
}

public record Trade(DateTime Time, double Price, double Quantity);
public record Candle(DateTime Time, double Open, double High, double Low, double Close, double Volume);


/// <summary>
/// Класс для построения одной свечи в реальном времени для конкретного интервала.
/// </summary>
public class CandleBuilder
{
    private readonly TimeSpan _interval;
    private Candle? _currentCandle;

    // Событие, которое срабатывает, когда свеча закрывается (завершается)
#pragma warning disable CA1003 // Use generic event handler instances
    public event Action<Candle>? OnCandleClosed;
#pragma warning restore CA1003 // Use generic event handler instances

    public CandleBuilder(TimeSpan interval)
    {
        _interval = interval;
    }

    public void ProcessTrade(Trade trade)
    {
        var windowStart = GetWindowStart(trade.Time, _interval);

        if (_currentCandle == null)
        {
            // Инициализация самой первой свечи
            StartNewCandle(windowStart, trade);
        }
        else if (windowStart > _currentCandle.Time)
        {
            // Сделка пришла из нового интервала -> закрываем старую свечу и открываем новую
            OnCandleClosed?.Invoke(_currentCandle);
            StartNewCandle(windowStart, trade);
        }
        else if (windowStart == _currentCandle.Time)
        {
            // Сделка относится к текущей свече -> обновляем её данные
            UpdateCurrentCandle(trade);
        }
        else
        {
            // Обработка сделок, пришедших с опозданием (out-of-order). 
            // В простых системах их либо игнорируют, либо логируют.
            Console.WriteLine($"Warning: Trade arrived too late for window {windowStart}");
        }
    }

    private void StartNewCandle(DateTime time, Trade trade)
    {
        _currentCandle = new Candle(
            Time: time,
            Open: trade.Price,
            High: trade.Price,
            Low: trade.Price,
            Close: trade.Price,
            Volume: trade.Quantity
        );
    }

    private void UpdateCurrentCandle(Trade trade)
    {
        // Поскольку record неизменяем, создаем копию с обновленными полями (with-выражение)
        _currentCandle = _currentCandle! with
        {
            High = Math.Max(_currentCandle!.High, trade.Price),
            Low = Math.Min(_currentCandle!.Low, trade.Price),
            Close = trade.Price,
            Volume = _currentCandle!.Volume + trade.Quantity
        };
    }

    private DateTime GetWindowStart(DateTime time, TimeSpan interval)
    {
        return new DateTime(time.Ticks - (time.Ticks % interval.Ticks), time.Kind);
    }

    // Метод для получения текущего состояния свечи (например, для отрисовки в UI до её закрытия)
    public Candle? GetCurrentCandle() => _currentCandle;
}

/// <summary>
/// Менеджер, который управляет несколькими интервалами одновременно.
/// </summary>
public class RealTimeAggregator
{
    private readonly List<CandleBuilder> _builders = new();

    public RealTimeAggregator(params TimeSpan[] intervals)
    {
        foreach (var interval in intervals)
        {
            var builder = new CandleBuilder(interval);
            // Подписываемся на событие закрытия свечи для каждого интервала
            builder.OnCandleClosed += (candle) =>
                Console.WriteLine($"[CLOSED] Interval {interval} | Time: {candle.Time:HH:mm:ss} | O:{candle.Open} C:{candle.Close} V:{candle.Volume}");

            _builders.Add(builder);
        }
    }

    public void AddTrade(Trade trade)
    {
        foreach (var builder in _builders)
        {
            builder.ProcessTrade(trade);
        }
    }
}