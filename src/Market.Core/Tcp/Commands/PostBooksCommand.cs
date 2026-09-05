using Market.ApiClient.Dtos;
using Market.ApiClient.Tcp;
using Market.Core.Abstractions;
using Market.Core.Converters;
using Market.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Primitives;

namespace Market.Core.Tcp.Commands;

internal sealed class PostBooksCommand : CommandBase
{
    public override CommandType CommandType => CommandType.PostBooks;

    private readonly IObjectStore<MarketDepth> _objectStore;

    public PostBooksCommand(
        IServiceScope serviceScope,
        TcpResponseWriter responseWriter) : base(serviceScope, responseWriter)
    {
        _objectStore = serviceScope.ServiceProvider.GetRequiredService<IObjectStore<MarketDepth>>();
    }

    public override async Task ExecuteAsync(int correlationId, byte[] payload, CancellationToken ct = default)
    {
        var dtos = MemoryPack.MemoryPackSerializer.Deserialize<MarketDepthDto[]>(payload);
        var books = dtos?.FromDto().ToArray();
        _objectStore.Set(books ?? []);

        await WriteEmptyResponse(correlationId, ct);
    }
}
