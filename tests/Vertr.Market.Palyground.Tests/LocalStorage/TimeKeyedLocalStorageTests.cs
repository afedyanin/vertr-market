using Vertr.Market.Application.LocalStorage;
using Vertr.Market.Palyground.Tests.Stubs;

namespace Vertr.Market.Palyground.Tests.LocalStorage;

[TestFixture(Category = "Unit")]
public class TimeKeyedLocalStorageTests
{
    private static readonly Guid InstrumentOne = Guid.NewGuid();
    private static readonly Guid InstrumentTwo = Guid.NewGuid();
    private static readonly DateTime BaseDate = new DateTime(2026, 03, 14, 20, 38, 12);

    [Test]
    public void CanGetLastItem()
    {
        var storage = new TimeKeyedLocalStorage<EntityStub>();

        var items = new List<EntityStub>();
        items.AddRange(EntityStub.GenerateItems(InstrumentOne, BaseDate, 3000));
        items.AddRange(EntityStub.GenerateItems(InstrumentTwo, BaseDate, 3000));

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
        var storage = new TimeKeyedLocalStorage<EntityStub>();

        var t1 = Task.Run(() =>
        {
            foreach (var item in EntityStub.GenerateItems(InstrumentOne, BaseDate, 3000))
            {
                storage.Add(item.InstrumentId, item);
            }
        });

        var t2 = Task.Run(() =>
        {
            foreach (var item in EntityStub.GenerateItems(InstrumentTwo, BaseDate, 3000))
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
}
