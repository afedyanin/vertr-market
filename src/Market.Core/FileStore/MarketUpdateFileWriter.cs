using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Market.Core.Models;

namespace Market.Core.FileStore;

public class MarketUpdateFileWriter : FileWriterBase
{
    public MarketUpdateFileWriter(string outputDirectory, TimeSpan flushInterval) : base(outputDirectory, flushInterval, "_trades.bin.gz")
    {
    }

    public void Write(in MarketUpdate marketUpdate)
    {
        var messageDate = DateTime.UnixEpoch.AddTicks(marketUpdate.MicrosecondTimestamp * 10);
        var dateKey = (messageDate.Year * 10000) + (messageDate.Month * 100) + messageDate.Day;
        var cacheKey = new StreamCacheKey(marketUpdate.AssetId, dateKey);
        var context = GetOrCreateContext(cacheKey);

        lock (context.LockObject)
        {
            ref readonly var depthRef = ref marketUpdate;
            var structSpan = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in depthRef), 1);
            var byteSpan = MemoryMarshal.AsBytes(structSpan);

            context.GzipStream.Write(byteSpan);
        }
    }
}
