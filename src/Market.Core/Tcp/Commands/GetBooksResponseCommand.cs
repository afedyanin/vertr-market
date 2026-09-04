using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Dtos;
using Market.Core.Abstractions;
using Market.Core.Converters;
using Market.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Market.Core.Tcp.Commands;

internal sealed class GetBooksResponseCommand : CommandResponseBase
{
    private readonly IObjectStore<MarketDepth> _objectStore;

    private readonly ILogger<GetBooksResponseCommand> _logger;

    public GetBooksResponseCommand(
        IServiceScope serviceScope,
        TcpResponseWriter responseWriter) : base(serviceScope, responseWriter)
    {
        _objectStore = serviceScope.ServiceProvider.GetRequiredService<IObjectStore<MarketDepth>>();
        _logger = serviceScope.ServiceProvider.GetRequiredService<ILogger<GetBooksResponseCommand>>();
    }

    public override CommandType CommandType => CommandType.GetBooksResponse;

    public override async Task ExecuteAsync(int correlationId, byte[] payload, CancellationToken ct = default)
    {
        var request = MemoryPack.MemoryPackSerializer.Deserialize<GetBooksRequestDto>(payload);

        if (request == null)
        {
            _logger.LogWarning("Cannot Deserialize GetBooksRequestDto. CorrelationId={CorrelationId}", correlationId);
            return;
        }

        var books = _objectStore.Get(request.AssetId, request.Count);
        var result = books.ToDto().ToArray();

        var responsePayload = MemoryPack.MemoryPackSerializer.Serialize(result);
        int totalLength = TcpConsts.MessageHeaderSize + responsePayload.Length;

        await ResponseWriter.WriteAsync(CommandType, totalLength, correlationId, responsePayload, ct);
    }
}
