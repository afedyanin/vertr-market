using Market.Core.Extensions;
using Market.Core.FileStore;
using Market.Core.Models;

namespace Market.Core.Tests.FileStore;

[TestFixture(Category = "Integration")]
internal sealed class OrderBookReaderTests
{
    [TestCase("Data\\1_2026-10-05_books.bin.gz")]
    public async Task CanReadMarketDepthBatchFromFile(string filePath)
    {
        using var cts = new CancellationTokenSource();

        try
        {
            await foreach (var book in OrderBookFileReader.ReadAsync(filePath, cts.Token))
            {
                ProcessDepth(book);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Чтение было отменено.");
        }
    }

    private void ProcessDepth(OrderBookDepth marketDepth)
    {
        Console.WriteLine(marketDepth.Dump());
    }
}
