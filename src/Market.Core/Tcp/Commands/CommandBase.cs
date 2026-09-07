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

    public abstract Task ExecuteAsync(
        TcpResponseWriter responseWriter,
        int correlationId,
        byte[] payload,
        CancellationToken ct = default);

    protected virtual async Task WriteEmptyResponse(
        TcpResponseWriter responseWriter,
        int correlationId,
        CancellationToken ct = default)
    {
        var responsePayload = MemoryPack.MemoryPackSerializer.Serialize(new EmptyDto());
        int totalLength = TcpConsts.MessageHeaderSize + responsePayload.Length;
        await responseWriter.WriteAsync(CommandType, totalLength, correlationId, responsePayload, ct);
    }
}
