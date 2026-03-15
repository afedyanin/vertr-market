using Microsoft.AspNetCore.Mvc;
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
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        from = AdjustTime(from, beginOfDay: true);
        to = AdjustTime(to, beginOfDay: false);
        var books = _orderBookRepository.Get(instrumentId, from.Value, to.Value);
        var stream = await CsvStreamExporter.ToStream(books, cancellationToken);

        return new FileStreamResult(stream, "text/csv")
        {
            FileDownloadName = GetFileName("order_books", instrumentId, from.Value, to.Value)
        };
    }

    [HttpGet("trades/{instrumentId:guid}")]
    public async Task<IActionResult> ExportTrades(
        Guid instrumentId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        from = AdjustTime(from, beginOfDay: true);
        to = AdjustTime(to, beginOfDay: false);
        var trades = _marketTradeRepository.Get(instrumentId, from.Value, to.Value);
        var stream = await CsvStreamExporter.ToStream(trades, cancellationToken);

        return new FileStreamResult(stream, "text/csv")
        {
            FileDownloadName = GetFileName("trades", instrumentId, from.Value, to.Value)
        };
    }

    [HttpGet("open-interests/{instrumentId:guid}")]
    public async Task<IActionResult> ExportOpenInterests(
        Guid instrumentId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        from = AdjustTime(from, beginOfDay: true);
        to = AdjustTime(to, beginOfDay: false);
        var trades = _openInterestRepository.Get(instrumentId, from.Value, to.Value);
        var stream = await CsvStreamExporter.ToStream(trades, cancellationToken);

        return new FileStreamResult(stream, "text/csv")
        {
            FileDownloadName = GetFileName("open_interests", instrumentId, from.Value, to.Value)
        };
    }

    private static string GetFileName(string prefix, Guid instrumentId, DateTime from, DateTime to)
        => $"{prefix}_{instrumentId}_{from.ToString(TimeFormat)}_{to.ToString(TimeFormat)}.csv";

    private static DateTime AdjustTime(DateTime? time, bool beginOfDay = false)
        => DateTime.SpecifyKind(time ?? (beginOfDay ? DateTime.Today : DateTime.UtcNow), kind: DateTimeKind.Utc);
}
