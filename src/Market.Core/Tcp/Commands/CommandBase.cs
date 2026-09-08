using System.Buffers;
using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Dtos;
using Market.Core.Abstractions;
using Market.Core.Models;

namespace Market.Core.Tcp.Commands;

public abstract class CommandBase
{
    private static readonly byte[] EmptyPayload = MemoryPack.MemoryPackSerializer.Serialize(new EmptyDto());
    private static readonly int EmptyTotalLength = TcpConsts.MessageHeaderSize + EmptyPayload.Length;

    protected IObjectStore<MarketDepth> BooksStore { get; private set; }

    public abstract CommandType CommandType { get; }

    protected CommandBase(IObjectStore<MarketDepth> booksStore)
    {
        BooksStore = booksStore;
    }

    public abstract Task ExecuteAsync(
        TcpResponseWriter responseWriter,
        int correlationId,
        ReadOnlySequence<byte> payload,
        CancellationToken ct = default);

    protected virtual async Task WriteEmptyResponse(
        TcpResponseWriter responseWriter,
        int correlationId,
        CancellationToken ct = default)
    {
        await responseWriter.WriteAsync(CommandType, EmptyTotalLength, correlationId, EmptyPayload, ct);
    }
}
