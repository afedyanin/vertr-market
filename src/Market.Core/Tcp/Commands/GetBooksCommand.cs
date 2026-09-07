using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Dtos;
using Market.Core.Abstractions;
using Market.Core.Converters;
using Market.Core.Models;
using ReactiveUI.Primitives;

namespace Market.Core.Tcp.Commands;

internal sealed class GetBooksCommand : CommandBase
{
    public override CommandType CommandType => CommandType.GetBooksResponse;

    public GetBooksCommand(IObjectStore<MarketDepth> booksStore) : base(booksStore)
    {
    }

    public override async Task ExecuteAsync(
        TcpResponseWriter responseWriter,
        int correlationId,
        byte[] payload,
        CancellationToken ct = default)
    {
        var request = MemoryPack.MemoryPackSerializer.Deserialize<GetBooksRequestDto>(payload);
        byte[] responsePayload = [];

        if (request != null)
        {
            var books = BooksStore.Get(request.AssetId, request.Count);
            var result = books.ToDto().ToArray();
            responsePayload = MemoryPack.MemoryPackSerializer.Serialize(result);
        }

        int totalLength = TcpConsts.MessageHeaderSize + responsePayload.Length;
        await responseWriter.WriteAsync(CommandType, totalLength, correlationId, responsePayload, ct);
    }
}
