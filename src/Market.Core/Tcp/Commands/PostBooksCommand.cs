using Market.ApiClient.Dtos;
using Market.ApiClient.Tcp;
using Market.Core.Abstractions;
using Market.Core.Converters;
using Market.Core.Models;
using ReactiveUI.Primitives;

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
        byte[] payload,
        CancellationToken ct = default)
    {
        var dtos = MemoryPack.MemoryPackSerializer.Deserialize<MarketDepthDto[]>(payload);
        var books = dtos?.FromDto().ToArray();
        BooksStore.Set(books ?? []);

        await WriteEmptyResponse(responseWriter, correlationId, ct);
    }
}
