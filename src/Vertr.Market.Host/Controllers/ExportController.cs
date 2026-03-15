using Microsoft.AspNetCore.Mvc;
using Vertr.Market.Application.Export;

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

        public byte[] Buffer { get; set; } = [];
    }


    [HttpGet("order-books")]
    public async Task<IActionResult> ExportOrderBooks(CancellationToken cancellationToken = default)
    {
        var items = GenerateItems(Guid.NewGuid(), 1_000_000);
        var stream = await CsvStreamExporter.ToStream(items, cancellationToken);

        return new FileStreamResult(stream, "text/csv")
        {
            FileDownloadName = "large_export.csv"
        };
    }

    private static IEnumerable<Entity> GenerateItems(Guid key, int count)
    {
        var res = new List<Entity>(count);
        var time = DateTime.UtcNow;
        var bufferLength = 128;

        for (var i = 0; i < count; i++)
        {
            time = time.AddSeconds(1);
            var e = new Entity
            {
                TimeUtc = time,
                InstrumentId = key,
                Price = 10.0m + i,
                Buffer = new byte[bufferLength]
            };

            res.Add(e);
        }

        return res;
    }
}
