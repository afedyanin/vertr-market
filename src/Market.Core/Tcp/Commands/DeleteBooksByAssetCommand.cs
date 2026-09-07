using System.Buffers;
using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Dtos;
using Market.Core.Abstractions;
using Market.Core.Models;

namespace Market.Core.Tcp.Commands;

internal sealed class DeleteBooksByAssetCommand : CommandBase
{
    public override CommandType CommandType => CommandType.DeleteBooksByAsset;

    public DeleteBooksByAssetCommand(IObjectStore<MarketDepth> booksStore) : base(booksStore)
    {
    }

    public override async Task ExecuteAsync(
        TcpResponseWriter responseWriter,
        int correlationId,
        ReadOnlySequence<byte> payload,
        CancellationToken ct = default)
    {
        // Deserialized before the first await: the payload is pipe memory released on AdvanceTo.
        var request = TcpPayload.Deserialize<DeleteBooksRequestDto>(payload);

        if (request != null)
        {
            BooksStore.Delete(request.AssetId);
        }

        await WriteEmptyResponse(responseWriter, correlationId, ct);
    }
}
