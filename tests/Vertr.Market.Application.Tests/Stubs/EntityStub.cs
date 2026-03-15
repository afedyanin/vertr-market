using Vertr.Common.Contracts.Abstractions;

namespace Vertr.Market.Application.Tests.Stubs;

internal sealed record class EntityStub : ITimeKeyedItem
{
    public DateTime TimeUtc { get; set; }
    public Guid InstrumentId { get; set; }
    public decimal Price { get; set; }

    public static IEnumerable<EntityStub> GenerateItems(Guid key, DateTime baseDate, int count)
    {
        var res = new List<EntityStub>(count);
        var time = baseDate;

        for (var i = 0; i < count; i++)
        {
            time = time.AddSeconds(1);
            var e = new EntityStub
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
