namespace Vertr.Market.Palyground.Tests.Stuff;

[TestFixture(Category = "Unit")]
public class LinqExtensionsTests
{
    private sealed record class Series
    {
        public DateTime Time { get; set; }

        public double Value { get; set; }
    }

    [Test]
    public void CanJoinTwoSequences()
    {
        var from = new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc);
        var s1 = GenerateSeries(from, 20, TimeSpan.FromMinutes(1)).ToArray();
        var s2 = GenerateSeries(from, 30, TimeSpan.FromSeconds(30)).ToArray();

        var s3 = s1.Join(s2,
            k1 => k1.Time,
            k2 => k2.Time,
            (a1, a2) => new
            {
                Time = a1.Time,
                Val1 = a1.Value,
                Val2 = a2.Value
            })
            .ToArray();

        for (var i = 0; i < s3.Length; i++)
        {
            var s1Found = s1.First(s => s.Time == s3[i].Time);
            var s2Found = s2.First(s => s.Time == s3[i].Time);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(s3[i].Time, Is.EqualTo(s1Found.Time));
                Assert.That(s3[i].Time, Is.EqualTo(s2Found.Time));
                Assert.That(s3[i].Val1, Is.EqualTo(s1Found.Value));
                Assert.That(s3[i].Val2, Is.EqualTo(s2Found.Value));
            }
        }
    }

    private static IEnumerable<Series> GenerateSeries(DateTime from, int count, TimeSpan step)
    {
        var currentTime = from;

        for (var i = 0; i < count; i++)
        {
            yield return new Series
            {
                Time = currentTime,
                Value = Random.Shared.NextDouble()
            };

            currentTime += step;
        }
    }
}
