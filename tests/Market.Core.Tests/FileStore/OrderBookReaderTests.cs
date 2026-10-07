using Market.Core.FileStore;
using Market.Core.Models;

namespace Market.Core.Tests.FileStore;

[TestFixture(Category = "Integration")]
internal sealed class OrderBookReaderTests
{
    [TestCase("Data\\books_2026-10-05.bin.gz")]
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
        if (marketDepth.AssetId == 6)
        {
            Console.WriteLine(marketDepth);
        }
    }
}
