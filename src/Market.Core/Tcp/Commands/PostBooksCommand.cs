using Market.ApiClient.Dtos;
using System.Buffers;
using Market.ApiClient.Tcp;
using Market.Core.Abstractions;
using Market.Core.Converters;
using Market.Core.Models;

namespace Market.Core.Tcp.Commands;

internal sealed class PostBooksCommand : CommandBase
{
    public override CommandType CommandType => CommandType.PostBooks;

    public PostBooksCommand(IObjectStore<MarketDepth> booksStore) : base(booksStore)
    {
    }

    public override async Task ExecuteAsync(
        TcpResponseWriter responseWriter,
        int correlationId,
        ReadOnlySequence<byte> payload,
        CancellationToken ct = default)
    {
        // Deserialized before the first await: the payload is pipe memory released on AdvanceTo.
        var dtos = TcpPayload.Deserialize<MarketDepthDto[]>(payload);
        var books = dtos?.FromDto().ToArray();
        BooksStore.Set(books ?? []);

        await WriteEmptyResponse(responseWriter, correlationId, ct);
    }
}
