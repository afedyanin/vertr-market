using System.Globalization;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;

namespace Vertr.Market.Host.Controllers;

[Route("api/export")]
[ApiController]
public class ExportController : ControllerBase
{
    private sealed record class Entity
    {
        public DateTime TimeUtc { get; set; }
        public Guid InstrumentId { get; set; }
        public decimal Price { get; set; }
    }


    [HttpGet("order-books")]
    public async Task<IActionResult> ExportOrderBooks(CancellationToken cancellationToken = default)
    {
        var items = GenerateItems(Guid.NewGuid(), 100);

        using var memoryStream = new MemoryStream();
        await using var streamWriter = new StreamWriter(memoryStream);
        await using var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture);

        csvWriter.WriteRecords(items);
        await streamWriter.FlushAsync(cancellationToken);

        return File(memoryStream.ToArray(), "text/csv", "export.csv");
    }

    private static IEnumerable<Entity> GenerateItems(Guid key, int count)
    {
        var res = new List<Entity>(count);
        var time = DateTime.UtcNow;

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
