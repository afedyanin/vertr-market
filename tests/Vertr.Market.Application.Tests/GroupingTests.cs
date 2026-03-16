
using Vertr.Market.Application.Tests.Stubs;

namespace Vertr.Market.Application.Tests;

[TestFixture(Category = "Unit")]
public class GroupingTests
{
    private static readonly Guid InstrumentId = Guid.NewGuid();
    private static readonly DateTime BaseDate = new DateTime(2026, 03, 14, 20, 38, 12);

    [Test]
    public void CanGroupByMinute()
    {
        var items = EntityStub.GenerateItems(InstrumentId, BaseDate, 300);
        var grouped = items.GroupBy(x => GroupByMinutes(x.TimeUtc));
        DumpGroups(grouped);
    }

    [TestCase(10)]
    [TestCase(5)]
    [TestCase(3)]
    [TestCase(2)]
    public void CanGroupBySec(int step)
    {
        var items = EntityStub.GenerateItems(InstrumentId, BaseDate, 300);
        var grouped = items.GroupBy(x => GroupBySec(x.TimeUtc, step));
        DumpGroups(grouped);
    }

    [Test]
    public void CanAggregateValues()
    {
        var items = EntityStub.GenerateItems(InstrumentId, BaseDate, 300);
        var aggregated = items
            .GroupBy(x => GroupBySec(x.TimeUtc, 5))
            .Select(g => new EntityStub
            {
                TimeUtc = g.Key,
                InstrumentId = InstrumentId,
                Price = g.Average(s => s.Price),
            });

        foreach (var item in aggregated)
        {
            Console.WriteLine($"--> {item.TimeUtc:O} P={item.Price}");
        }
    }

    [Test]
    public void CanCalculateStdDev()
    {
        var items = EntityStub.GenerateItems(InstrumentId, BaseDate, 300);
        var aggregated = items
            .GroupBy(x => GroupBySec(x.TimeUtc, 5))
            .Select(g =>
            {
                var avg = g.Average(s => s.Price);
                var count = g.Count();
                var sumOfSquares = g.Sum(v => (v.Price - avg) * (v.Price - avg));
                var stdDev = count <= 0 ? 0 : Math.Sqrt((double)sumOfSquares / count);

                return new
                {
                    TimeUtc = g.Key,
                    InstrumentId = InstrumentId,
                    ClosePrice = g.Last().Price,
                    AvgPrice = avg,
                    StdDev = stdDev,
                    Count = count
                };
            });

        foreach (var item in aggregated)
        {
            Console.WriteLine($"--> {item.TimeUtc:O} Close={item.ClosePrice} Avg={item.AvgPrice} StdDev={item.StdDev} Count={item.Count}");
        }
    }

    [Test]
    public void CanUseIntDiv()
    {
        Assert.That(IntDiv(23, 10), Is.EqualTo(20));
        Assert.That(IntDiv(13, 5), Is.EqualTo(10));
        Assert.That(IntDiv(17, 5), Is.EqualTo(15));
        Assert.That(IntDiv(23, 5), Is.EqualTo(20));
    }

    private static void DumpGroups(IEnumerable<IGrouping<DateTime, EntityStub>> grouped)
    {
        foreach (var group in grouped)
        {
            Console.WriteLine($"Group time: {group.Key:O}");

            foreach (var item in group)
            {
                Console.WriteLine($"--> {item.TimeUtc:O}");
            }

            Console.WriteLine("=====================");
        }
    }

    private static DateTime GroupByMinutes(DateTime dt)
        => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, DateTimeKind.Utc);

    private static DateTime GroupBySec(DateTime dt, int stepSec)
        => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, IntDiv(dt.Second, stepSec), DateTimeKind.Utc);

    private static int IntDiv(int number, int part) => (number / part) * part;
}
