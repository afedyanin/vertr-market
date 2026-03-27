using Vertr.Market.Application.Export;

namespace Vertr.Market.DataAccess.Tests;

[TestFixture(Category = "Database", Explicit = true)]
public class MarketTradeRepositoryTests : DataAccessTestBase
{
    private static readonly Guid Sber = new Guid("e6123145-9665-43e0-8413-cd61b8aa9b13");
    //private static readonly Guid Srm6 = new Guid("755574b5-703f-4a2f-9e1d-137c8cb90850"); // SRM6: SBRF-6.26 Сбер Банк (обыкновенные)
    //private static readonly Guid Sru6 = new Guid("f532beef-65bb-4a42-894c-e9bc728f93c4"); // SRU6: SBRF-9.26 Сбер Банк (обыкновенные)

    private static readonly DateTime From = new DateTime(2026, 03, 24, 4, 0, 0, DateTimeKind.Utc);
    // private static readonly DateTime To = new DateTime(2026, 03, 24, 4, 10, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new DateTime(2026, 03, 24, 23, 59, 59, DateTimeKind.Utc);

    private const int AggregationIntervalSec = 30;
    private const int StdDevIntervals = 3;

    [Test]
    public async Task CanGetTrades()
    {
        var count = 0;
        await foreach (var trade in MarketTradeRepository.Get(Sber, From, To))
        {
            Console.WriteLine(trade);
            count++;

            if (count > 10)
            {
                break;
            }
        }
    }

    [Test]
    public void CanGetBasicStats()
    {
        var items = Enumerable.Range(0, 100).Select(t => (double)t);
        var stats = GetBasicStats(items);

        Console.WriteLine($"stats={stats}");
    }

    [Test]
    public void CanNormalizeItems()
    {
        var items = Enumerable.Range(0, 100).Select(t => Random.Shared.NextDouble());
        var stats = GetBasicStats(items);
        var normalized = Normalize(items, stats.Item1, stats.Item2);

        Console.WriteLine($"stats={stats.Item1:N6}+-{stats.Item2:N6}");

        foreach (var item in normalized)
        {
            Console.WriteLine($"{item}");
        }
    }

    private static IEnumerable<double> Normalize(IEnumerable<double> items, double mean, double stddev)
        => items.Select(t => (t - mean) / stddev);

    private static (double, double) GetBasicStats(IEnumerable<double> items)
        => items.Aggregate(
            new { Count = 0, Sum = 0.0, SumSquares = 0.0 },
            (acc, x) => new
            {
                Count = acc.Count + 1,
                Sum = acc.Sum + x,
                SumSquares = acc.SumSquares + (x * x)
            },
            acc =>
            {
                var mean = acc.Sum / acc.Count;
                var variance = (acc.SumSquares - (acc.Sum * acc.Sum) / acc.Count) / acc.Count;
                return (mean, Math.Sqrt(variance));
            });

    [Test]
    public async Task CanAggregateTrades()
    {
        var trades = MarketTradeRepository.Get(Sber, From, To);

        var aggregated = await trades
            //.Take(5000)
            .GroupBy(x => CsvStreamExporter.GroupBySec(x.TimeUtc, AggregationIntervalSec))
            .Select(g =>
            {
                var ordered = g.OrderBy(t => t.TimeUtc);
                var open = ordered.First();
                var close = ordered.Last();
                var percentChange = open.Price == 0 ? 0 : (close.Price - open.Price) / open.Price;
                var value = g.Sum(s => s.Quantity * s.Price);

                return new
                {
                    TimeUtc = g.Key,
                    PercentChange = percentChange,
                    Value = value,
                };
            }).ToArrayAsync();

        var stats = aggregated.Aggregate(
            new
            {
                Count = 0,
                ChangeSum = 0.0,
                ChangeSumSquares = 0.0,
                ValSum = 0.0,
                ValSumSquares = 0.0
            },
            (acc, x) => new
            {
                Count = acc.Count + 1,
                ChangeSum = acc.ChangeSum + (double)x.PercentChange,
                ChangeSumSquares = acc.ChangeSumSquares + (double)(x.PercentChange * x.PercentChange),
                ValSum = acc.ValSum + (double)x.Value,
                ValSumSquares = acc.ValSumSquares + (double)(x.Value * x.Value)
            },
            acc =>
            {
                var changeMean = acc.ChangeSum / acc.Count;
                var changeVariance = (acc.ChangeSumSquares - (acc.ChangeSum * acc.ChangeSum) / acc.Count) / acc.Count;
                var valMean = acc.ValSum / acc.Count;
                var valVariance = (acc.ValSumSquares - (acc.ValSum * acc.ValSum) / acc.Count) / acc.Count;
                return new
                {
                    ChangeMean = changeMean,
                    ChangeStdDev = Math.Sqrt(changeVariance),
                    ValMean = valMean,
                    ValStdDev = Math.Sqrt(valVariance)
                };
            });

        // Console.WriteLine($"Stats: {stats}");

        var standartized = aggregated.Select(x => new
        {
            TimeUtc = x.TimeUtc,
            PercentChange = ((double)x.PercentChange - stats.ChangeMean) / stats.ChangeStdDev,
            Value = ((double)x.Value - stats.ValMean) / stats.ValStdDev,
        });

        // var stdChangedMean = standartized.Average(c => c.PercentChange);
        // var stdValueMean = standartized.Average(c => c.Value);
        // Console.WriteLine($"stdChangedMean={stdChangedMean:N10} stdValueMean={stdValueMean:N10}");

        var filtered = standartized.Where(x => Math.Abs(x.PercentChange) >= StdDevIntervals);

        Console.WriteLine($"count={filtered.Count()}");

        foreach (var item in filtered)
        {
            Console.WriteLine(item);
        }
    }
}
