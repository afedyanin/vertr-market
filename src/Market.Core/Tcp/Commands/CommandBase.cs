using System.Buffers;
using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Dtos;
using Market.Core.Abstractions;
using Market.Core.Models;

namespace Market.Core.Tcp.Commands;

public abstract class CommandBase
{
    protected IObjectStore<MarketDepth> BooksStore { get; private set; }

    public abstract CommandType CommandType { get; }

    protected CommandBase(IObjectStore<MarketDepth> booksStore)
    {
        BooksStore = booksStore;
    }

    // `payload` is a slice of pipe memory: it must be deserialized synchronously before the
    // first await (see the lifetime contract in TcpCommandParser.ReadPipeAsync).
    public abstract Task ExecuteAsync(
        TcpResponseWriter responseWriter,
        int correlationId,
        ReadOnlySequence<byte> payload,
        CancellationToken ct = default);

    // The empty response body is identical for every command, so it is serialized once per
    // process instead of re-serializing EmptyDto on each PostBooks/ClearBooks/DeleteBooks reply.
    private static readonly byte[] EmptyPayload = MemoryPack.MemoryPackSerializer.Serialize(new EmptyDto());
    private static readonly int EmptyTotalLength = TcpConsts.MessageHeaderSize + EmptyPayload.Length;

    protected virtual async Task WriteEmptyResponse(
        TcpResponseWriter responseWriter,
        int correlationId,
        CancellationToken ct = default)
    {
        await responseWriter.WriteAsync(CommandType, EmptyTotalLength, correlationId, EmptyPayload, ct);
    }
}
