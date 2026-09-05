using Market.ApiClient.Tcp;
using Market.Core.Abstractions;
using Market.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Market.Core.Tcp.Commands;

internal sealed class ClearBooksCommand : CommandBase
{
    public override CommandType CommandType => CommandType.ClearBooks;

    private readonly IObjectStore<MarketDepth> _objectStore;

    public ClearBooksCommand(
        IServiceScope serviceScope,
        TcpResponseWriter responseWriter) : base(serviceScope, responseWriter)
    {
        _objectStore = serviceScope.ServiceProvider.GetRequiredService<IObjectStore<MarketDepth>>();
    }

    public override async Task ExecuteAsync(int correlationId, byte[] payload, CancellationToken ct = default)
    {
        _objectStore.Clear();
        await WriteEmptyResponse(correlationId, ct);
    }
}
