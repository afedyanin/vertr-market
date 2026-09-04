using Market.ApiClient.Tcp;
using Microsoft.Extensions.DependencyInjection;

namespace Market.Core.Tcp.Commands;

internal sealed class PostBooksResponseCommand : CommandResponseBase
{
    public PostBooksResponseCommand(
        IServiceScope serviceScope,
        TcpResponseWriter responseWriter) : base(serviceScope, responseWriter)
    {
    }

    public override CommandType CommandType => CommandType.PostBooks;

    public override Task ExecuteAsync(int correlationId, byte[] payload, CancellationToken ct = default)
    {
        //var books = MemoryPack.MemoryPackSerializer.Deserialize<MarketDepthDto[]>(payload);
        // Бизнес-логика...

        return Task.CompletedTask;
    }
}
