using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Market.Core.Abstractions;
using Market.Core.Models;

namespace Market.Core.FileStore;

public class MarketDepthFileWriter : FileWriterBase, IMarketDepthWriter
{
    public MarketDepthFileWriter(string outputDirectory, TimeSpan flushInterval) : base(outputDirectory, flushInterval, "_books.bin.gz")
    {
    }

    public void Write(in MarketDepth depth)
    {
        var messageDate = DateTime.UnixEpoch.AddTicks(depth.MicrosecondTimestamp * 10);
        var dateKey = (messageDate.Year * 10000) + (messageDate.Month * 100) + messageDate.Day;
        var cacheKey = new StreamCacheKey(depth.AssetId, dateKey);
        var context = GetOrCreateContext(cacheKey);

        lock (context.LockObject)
        {
            ref readonly var depthRef = ref depth;
            var structSpan = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in depthRef), 1);
            var byteSpan = MemoryMarshal.AsBytes(structSpan);

            context.GzipStream.Write(byteSpan);
        }
    }
}
