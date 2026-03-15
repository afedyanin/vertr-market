using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Vertr.Market.Application.Abstractions;
using Vertr.Market.Application.Export;

namespace Vertr.Market.Host.Controllers;

[Route("api/export")]
[ApiController]
public class ExportController : ControllerBase
{
    private const string TimeFormat = "yyyy-MM-ddTHH-mm-ss";

    private readonly IOrderBookRepository _orderBookRepository;
    private readonly IMarketTradeRepository _marketTradeRepository;
    private readonly IOpenInterestRepository _openInterestRepository;

    public ExportController(
        IOrderBookRepository orderBookRepository,
        IMarketTradeRepository marketTradeRepository,
        IOpenInterestRepository openInterestRepository)
    {
        _orderBookRepository = orderBookRepository;
        _marketTradeRepository = marketTradeRepository;
        _openInterestRepository = openInterestRepository;
    }

    [HttpGet("order-books/{instrumentId:guid}")]
    public async Task<IActionResult> ExportOrderBooks(
        Guid instrumentId,
        [BindRequired][FromQuery] DateTime from,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        from = AdjustTime(from);
        to = AdjustTime(to);
        var books = _orderBookRepository.Get(instrumentId, from, to.Value);
        var stream = await CsvStreamExporter.ToStream(books, cancellationToken);

        return new FileStreamResult(stream, "text/csv")
        {
            FileDownloadName = GetFileName("order_books", instrumentId, from, to.Value)
        };
    }

    [HttpGet("trades/{instrumentId:guid}")]
    public async Task<IActionResult> ExportTrades(
        Guid instrumentId,
        [BindRequired][FromQuery] DateTime from,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        from = AdjustTime(from);
        to = AdjustTime(to);
        var trades = _marketTradeRepository.Get(instrumentId, from, to.Value);
        var stream = await CsvStreamExporter.ToStream(trades, cancellationToken);

        return new FileStreamResult(stream, "text/csv")
        {
            FileDownloadName = GetFileName("trades", instrumentId, from, to.Value)
        };
    }

    [HttpGet("open-interests/{instrumentId:guid}")]
    public async Task<IActionResult> ExportOpenInterests(
        Guid instrumentId,
        [BindRequired][FromQuery] DateTime from,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        from = AdjustTime(from);
        to = AdjustTime(to);
        var trades = _openInterestRepository.Get(instrumentId, from, to.Value);
        var stream = await CsvStreamExporter.ToStream(trades, cancellationToken);

        return new FileStreamResult(stream, "text/csv")
        {
            FileDownloadName = GetFileName("open_interests", instrumentId, from, to.Value)
        };
    }

    private static string GetFileName(string prefix, Guid instrumentId, DateTime from, DateTime to)
        => $"{prefix}_{instrumentId}_{from.ToString(TimeFormat)}_{to.ToString(TimeFormat)}.csv";

    private static DateTime AdjustTime(DateTime? time)
        => DateTime.SpecifyKind(time ?? DateTime.UtcNow, kind: DateTimeKind.Utc);
}
