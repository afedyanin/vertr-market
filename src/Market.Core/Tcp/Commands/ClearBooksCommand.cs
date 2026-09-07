using System.Buffers;
using Market.ApiClient.Tcp;
using Market.Core.Abstractions;
using Market.Core.Models;

namespace Market.Core.Tcp.Commands;

internal sealed class ClearBooksCommand : CommandBase
{
    public override CommandType CommandType => CommandType.ClearBooks;

    public ClearBooksCommand(IObjectStore<MarketDepth> booksStore) : base(booksStore)
    {
    }

    public override async Task ExecuteAsync(
        TcpResponseWriter responseWriter,
        int correlationId,
        ReadOnlySequence<byte> payload,
        CancellationToken ct = default)
    {
        BooksStore.Clear();
        await WriteEmptyResponse(responseWriter, correlationId, ct);
    }
}
