using System.Globalization;
using CsvHelper;
using Vertr.Common.Contracts;

namespace Vertr.Market.Application.Export;

public static class CsvStreamExporter
{
    public static async Task<Stream> ToStream<T>(IEnumerable<T> records, CancellationToken cancellationToken = default) where T : class
    {
        var memoryStream = new MemoryStream();
        await using var streamWriter = new StreamWriter(memoryStream, leaveOpen: true);
        await using var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture, leaveOpen: true);

        csvWriter.WriteRecords(records);
        await streamWriter.FlushAsync(cancellationToken);
        memoryStream.Position = 0;

        return memoryStream;
    }

    public static async Task<Stream> ToStream(IAsyncEnumerable<OrderBook> books, CancellationToken cancellationToken = default)
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

    public static async Task<Stream> ToStream(IAsyncEnumerable<MarketTrade> trades, CancellationToken cancellationToken = default)
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

    public static async Task<Stream> ToStream(IAsyncEnumerable<OpenInterest> interests, CancellationToken cancellationToken = default)
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
}
