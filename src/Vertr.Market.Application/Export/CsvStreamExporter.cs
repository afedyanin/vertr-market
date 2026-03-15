using System.Globalization;
using CsvHelper;

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
}
