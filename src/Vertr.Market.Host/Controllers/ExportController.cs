using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.Application.Export;

namespace Vertr.Market.Host.Controllers;

[Route("api/export")]
[ApiController]
public class ExportController : ControllerBase
{
    private readonly IOrderBookRepository _orderBookRepository;

    public ExportController(IOrderBookRepository orderBookRepository)
    {
        _orderBookRepository = orderBookRepository;
    }

    [HttpGet("order-books/{instrumentId:guid}")]
    public async Task<IActionResult> ExportOrderBooks(
        Guid instrumentId,
        [BindRequired][FromQuery] DateTime from,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var toTime = to ?? DateTime.UtcNow;
        var books = _orderBookRepository.Get(instrumentId, from, toTime);
        var stream = await CsvStreamExporter.ToStream(books, cancellationToken);

        var fromTimeString = from.ToString("O").Replace(":", "_");
        var toTimeString = toTime.ToString("O").Replace(":", "_");
        var fileName = $"order_books_{instrumentId}_{fromTimeString}_{toTimeString}.csv";

        return new FileStreamResult(stream, "text/csv")
        {
            FileDownloadName = fileName
        };
    }
}
