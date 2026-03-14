using Vertr.Common.Contracts.Abstractions;
using Vertr.Market.Application.LocalStorage;

namespace Vertr.Market.Application.Tests.LocalStorage;

[TestFixture(Category = "Unit")]
public class TimeKeyedLocalStorageTests
{
    private static readonly Guid InstrumentOne = Guid.NewGuid();
    private static readonly Guid InstrumentTwo = Guid.NewGuid();
    private static readonly DateTime BaseDate = new DateTime(2026, 03, 14, 20, 38, 12);

    private sealed record class Entity : ITimeKeyedItem
    {
        public DateTime TimeUtc { get; set; }
        public Guid InstrumentId { get; set; }
        public decimal Price { get; set; }
    }

    [Test]
    public void CanGetLastItem()
    {
        var storage = new TimeKeyedLocalStorage<Entity>();

        var items = new List<Entity>();
        items.AddRange(GenerateItems(InstrumentOne, 3000));
        items.AddRange(GenerateItems(InstrumentTwo, 3000));

        Parallel.ForEach(items, item =>
        {
            storage.Add(item.InstrumentId, item);
        });

        var last1 = storage.GetLast(InstrumentOne);
        var expected1 = items.Where(item => item.InstrumentId == InstrumentOne).OrderBy(e => e.TimeUtc).Last();
        Assert.That(last1, Is.EqualTo(expected1));

        var last2 = storage.GetLast(InstrumentTwo);
        var expected2 = items.Where(item => item.InstrumentId == InstrumentTwo).OrderBy(e => e.TimeUtc).Last();
        Assert.That(last2, Is.EqualTo(expected2));
    }

    [Test]
    public async Task CanRemoveItems()
    {
        var storage = new TimeKeyedLocalStorage<Entity>();

        var t1 = Task.Run(() =>
        {
            foreach (var item in GenerateItems(InstrumentOne, 3000))
            {
                storage.Add(item.InstrumentId, item);
            }
        });

        var t2 = Task.Run(() =>
        {
            foreach (var item in GenerateItems(InstrumentTwo, 3000))
            {
                storage.Add(item.InstrumentId, item);
            }
        });

        await Task.WhenAll(t1, t2);

        var beforeTime = BaseDate.AddSeconds(1001);

        var removed1 = storage.RemoveBefore(InstrumentOne, beforeTime);
        Assert.That(removed1.Count, Is.EqualTo(1000));

        var removed2 = storage.RemoveBefore(InstrumentTwo, beforeTime);
        Assert.That(removed2.Count, Is.EqualTo(1000));
    }

    private static IEnumerable<Entity> GenerateItems(Guid key, int count)
    {
        var res = new List<Entity>(count);
        var time = BaseDate;

        for (var i = 0; i < count; i++)
        {
            time = time.AddSeconds(1);
            var e = new Entity
            {
                TimeUtc = time,
                InstrumentId = key,
                Price = 10.0m + i,
            };

            res.Add(e);
        }

        return res;
    }
}
