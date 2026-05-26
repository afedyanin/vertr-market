namespace Vertr.Market.Palyground.Tests.Stuff;

[TestFixture(Category = "Unit")]
public class DateTimeTests
{
    [Test]
    public void CanAdjustTime()
    {
        var bodAdjusted = AdjustTime(null, beginOfDay: true);
        Assert.That(bodAdjusted, Is.EqualTo(DateTime.Today));

        var now = DateTime.UtcNow;
        var nowAdjusted = AdjustTime(now);
        Assert.That(nowAdjusted, Is.EqualTo(now));

        var currentAdjusted = AdjustTime(null, beginOfDay: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(currentAdjusted.Day, Is.EqualTo(now.Day));
            Assert.That(currentAdjusted.Month, Is.EqualTo(now.Month));
            Assert.That(currentAdjusted.Year, Is.EqualTo(now.Year));
            Assert.That(currentAdjusted.Second, Is.GreaterThan(0));
            Assert.That(currentAdjusted.Minute, Is.GreaterThan(0));
            Assert.That(currentAdjusted.Hour, Is.GreaterThan(0));
        }

        Console.WriteLine($"BeginOfDay={bodAdjusted:O} NowFixed={nowAdjusted:O} Current={currentAdjusted:O}");
    }

    private static DateTime AdjustTime(DateTime? time, bool beginOfDay = false)
        => DateTime.SpecifyKind(time ?? (beginOfDay ? DateTime.Today : DateTime.UtcNow), kind: DateTimeKind.Utc);

}
