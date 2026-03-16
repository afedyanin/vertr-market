using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using Vertr.Common.Contracts;

namespace Vertr.Market.Application.Export;

public static class MarketTradesExporter
{
    private record class AggregatedMarketTrade
    {
        public DateTime TimeUtc { get; init; }
        public Guid InstrumentId { get; init; }
        public decimal PriceClose { get; init; }
        public decimal PriceAvg { get; init; }
        public double PriceStdDev { get; init; }
        public decimal SellVolume { get; init; }
        public decimal BuyVolume { get; init; }
        public decimal SellValue { get; init; }
        public decimal BuyValue { get; init; }
        public int Count { get; init; }
    }

    public static async Task<Stream> ToCsvStream(IAsyncEnumerable<MarketTrade> trades, CancellationToken cancellationToken = default)
    {
        var memoryStream = new MemoryStream();
        await using var streamWriter = new StreamWriter(memoryStream, leaveOpen: true);
        await using var csv = new CsvWriter(streamWriter, CultureInfo.InvariantCulture, leaveOpen: true);

        await foreach (var trade in trades.WithCancellation(cancellationToken))
        {
            csv.WriteField(trade.TimeUtc);
            csv.WriteField(trade.Price);
            csv.WriteField(trade.Quantity);
            csv.WriteField(trade.Direction);

            await csv.NextRecordAsync(); // Ends the row
        }

        await streamWriter.FlushAsync(cancellationToken);
        memoryStream.Position = 0;

        return memoryStream;
    }

    public static async Task<Stream> ToCsvStreamAggregated(
        IAsyncEnumerable<MarketTrade> trades,
        int aggregationIntervalSec = 30,
        CancellationToken cancellationToken = default)
    {
        Trace.Assert(aggregationIntervalSec > 0 && aggregationIntervalSec <= 60);

        var memoryStream = new MemoryStream();
        await using var streamWriter = new StreamWriter(memoryStream, leaveOpen: true);
        await using var csv = new CsvWriter(streamWriter, CultureInfo.InvariantCulture, leaveOpen: true);

        var aggregated = trades
            .GroupBy(x => CsvStreamExporter.GroupBySec(x.TimeUtc, aggregationIntervalSec))
            .Select(g =>
            {
                var last = g.OrderBy(t => t.TimeUtc).Last();
                var avg = g.Average(s => s.Price);
                var count = g.Count();
                var sumOfSquares = g.Sum(v => (v.Price - avg) * (v.Price - avg));
                var stdDev = count <= 0 ? 0 : Math.Sqrt((double)sumOfSquares / count);

                var sellVol = g.Where(s => s.Direction == TradingDirection.Sell).Sum(s => s.Quantity);
                var buyVol = g.Where(s => s.Direction == TradingDirection.Buy).Sum(s => s.Quantity);

                var sellVal = g.Where(s => s.Direction == TradingDirection.Sell).Sum(s => s.Quantity * s.Price);
                var buyVal = g.Where(s => s.Direction == TradingDirection.Buy).Sum(s => s.Quantity * s.Price);

                return new AggregatedMarketTrade
                {
                    TimeUtc = g.Key,
                    InstrumentId = last.InstrumentId,
                    PriceClose = last.Price,
                    PriceAvg = avg,
                    PriceStdDev = stdDev,
                    Count = count,
                    BuyVolume = sellVol,
                    SellVolume = sellVol,
                    BuyValue = buyVal,
                    SellValue = sellVal,
                };
            });

        csv.WriteHeader<AggregatedMarketTrade>();
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
