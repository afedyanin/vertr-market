using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Dtos;
using Market.Core.Abstractions;
using Market.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Market.Core.Tcp.Commands;

internal sealed class DeleteBooksByAssetCommand : CommandBase
{
    private readonly IObjectStore<MarketDepth> _objectStore;
    public override CommandType CommandType => CommandType.DeleteBooksByAsset;

    public DeleteBooksByAssetCommand(
        IServiceScope serviceScope,
        TcpResponseWriter responseWriter) : base(serviceScope, responseWriter)
    {
        _objectStore = serviceScope.ServiceProvider.GetRequiredService<IObjectStore<MarketDepth>>();

    }

    public override async Task ExecuteAsync(int correlationId, byte[] payload, CancellationToken ct = default)
    {
        var request = MemoryPack.MemoryPackSerializer.Deserialize<DeleteBooksRequestDto>(payload);

        if (request != null)
        {
            _objectStore.Delete(request.AssetId);
        }

        await WriteEmptyResponse(correlationId, ct);
    }
}
