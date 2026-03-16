using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using Vertr.Common.Contracts;

namespace Vertr.Market.Application.Export;

public static class OPenInterestsExporter
{
    private record class AggregatedOpenInterest
    {
        public DateTime TimeUtc { get; init; }
        public decimal QtyLast { get; init; }
        public double QtyAvg { get; init; }
        public double QtyStdDev { get; init; }
        public int Count { get; init; }
    }

    public static async Task<Stream> ToCsvStream(IAsyncEnumerable<OpenInterest> interests, CancellationToken cancellationToken = default)
    {
        var memoryStream = new MemoryStream();
        await using var streamWriter = new StreamWriter(memoryStream, leaveOpen: true);
        await using var csv = new CsvWriter(streamWriter, CultureInfo.InvariantCulture, leaveOpen: true);

        await foreach (var interest in interests.WithCancellation(cancellationToken))
        {
            csv.WriteField(interest.TimeUtc);
            csv.WriteField(interest.Quantity);
            await csv.NextRecordAsync(); // Ends the row
        }

        await streamWriter.FlushAsync(cancellationToken);
        memoryStream.Position = 0;

        return memoryStream;
    }

    public static async Task<Stream> ToCsvStreamAggregated(
        IAsyncEnumerable<OpenInterest> interests,
        int aggregationIntervalSec = 30,
        CancellationToken cancellationToken = default)
    {
        Trace.Assert(aggregationIntervalSec > 0 && aggregationIntervalSec <= 60);

        var memoryStream = new MemoryStream();
        await using var streamWriter = new StreamWriter(memoryStream, leaveOpen: true);
        await using var csv = new CsvWriter(streamWriter, CultureInfo.InvariantCulture, leaveOpen: true);

        var aggregated = interests
            .GroupBy(x => CsvStreamExporter.GroupBySec(x.TimeUtc, aggregationIntervalSec))
            .Select(g =>
            {
                var last = g.OrderBy(t => t.TimeUtc).Last();
                var avg = g.Average(s => s.Quantity);
                var count = g.Count();
                var sumOfSquares = g.Sum(v => (v.Quantity - avg) * (v.Quantity - avg));
                var stdDev = count <= 0 ? 0 : Math.Sqrt((double)sumOfSquares / count);

                return new AggregatedOpenInterest
                {
                    TimeUtc = g.Key,
                    QtyLast = last.Quantity,
                    QtyAvg = avg,
                    QtyStdDev = stdDev,
                    Count = count,
                };
            });

        csv.WriteHeader<AggregatedOpenInterest>();
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
