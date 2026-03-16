using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using Vertr.Common.Contracts;

namespace Vertr.Market.Application.Export;

public static class OrderBooksExporter
{
    private record class AggregatedBookItem
    {
        public DateTime TimeUtc { get; init; }
        public decimal MidPriceClose { get; init; }
        public decimal MidPriceAvg { get; init; }
        public double MidPriceStdDev { get; init; }
        public decimal SpreadAvg { get; init; }
        public int Count { get; init; }
    }

    public static async Task<Stream> ToCsvStream(
        IAsyncEnumerable<OrderBook> books,
        CancellationToken cancellationToken = default)
    {
        var memoryStream = new MemoryStream();
        await using var streamWriter = new StreamWriter(memoryStream, leaveOpen: true);
        await using var csv = new CsvWriter(streamWriter, CultureInfo.InvariantCulture, leaveOpen: true);

        await foreach (var book in books.WithCancellation(cancellationToken))
        {
            csv.WriteField(book.TimeUtc);

            foreach (var bid in book.Bids)
            {
                csv.WriteField(bid.Price);
                csv.WriteField(bid.QtyLots);
            }

            foreach (var ask in book.Asks)
            {
                csv.WriteField(ask.Price);
                csv.WriteField(ask.QtyLots);
            }

            await csv.NextRecordAsync(); // Ends the row
        }

        await streamWriter.FlushAsync(cancellationToken);
        memoryStream.Position = 0;

        return memoryStream;
    }

    public static async Task<Stream> ToCsvStreamAggregated(
        IAsyncEnumerable<OrderBook> books,
        int aggregationIntervalSec = 30,
        CancellationToken cancellationToken = default)
    {
        Trace.Assert(aggregationIntervalSec > 0 && aggregationIntervalSec <= 60);

        var memoryStream = new MemoryStream();
        await using var streamWriter = new StreamWriter(memoryStream, leaveOpen: true);
        await using var csv = new CsvWriter(streamWriter, CultureInfo.InvariantCulture, leaveOpen: true);

        var aggregated = books
            .GroupBy(x => CsvStreamExporter.GroupBySec(x.TimeUtc, aggregationIntervalSec))
            .Select(g =>
            {
                var last = g.OrderBy(t => t.TimeUtc).Last();
                var avg = g.Average(s => s.MidPrice);
                var count = g.Count();
                var sumOfSquares = g.Sum(v => (v.MidPrice - avg) * (v.MidPrice - avg));
                var stdDev = count <= 0 ? 0 : Math.Sqrt((double)sumOfSquares / count);
                var avgSpread = g.Average(s => s.BestAsk - s.BestBid);

                return new AggregatedBookItem
                {
                    TimeUtc = g.Key,
                    MidPriceClose = last.MidPrice,
                    MidPriceAvg = avg,
                    MidPriceStdDev = stdDev,
                    SpreadAvg = avgSpread,
                    Count = count,
                };
            });

        csv.WriteHeader<AggregatedBookItem>();
        await csv.NextRecordAsync();

        await foreach (var group in aggregated.WithCancellation(cancellationToken))
        {
            csv.WriteRecord(group);
            await csv.NextRecordAsync(); // Ends the row
        }

        await streamWriter.FlushAsync(cancellationToken);
        memoryStream.Position = 0;

        return memoryStream;
    }
}
