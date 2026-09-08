using System.Buffers;

namespace Market.Core.Tcp;

internal static class TcpPayload
{
    public static T? Deserialize<T>(ReadOnlySequence<byte> payload)
        where T : class
        => payload.IsSingleSegment
            ? MemoryPack.MemoryPackSerializer.Deserialize<T>(payload.First.Span)
            : DeserializeContiguous<T>(payload);

    private static T? DeserializeContiguous<T>(ReadOnlySequence<byte> payload)
        where T : class
    {
        byte[] buffer = new byte[payload.Length];
        payload.CopyTo(buffer);
        return MemoryPack.MemoryPackSerializer.Deserialize<T>(buffer);
    }
}
