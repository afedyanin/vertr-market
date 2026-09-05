using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Dtos;
using Market.Core.Abstractions;
using Market.Core.Converters;
using Market.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI.Primitives;

namespace Market.Core.Tcp.Commands;

internal sealed class GetBooksCommand : CommandBase
{
    private readonly IObjectStore<MarketDepth> _objectStore;

    private readonly ILogger<GetBooksCommand> _logger;

    public GetBooksCommand(
        IServiceScope serviceScope,
        TcpResponseWriter responseWriter) : base(serviceScope, responseWriter)
    {
        _objectStore = serviceScope.ServiceProvider.GetRequiredService<IObjectStore<MarketDepth>>();
        _logger = serviceScope.ServiceProvider.GetRequiredService<ILogger<GetBooksCommand>>();
    }

    public override CommandType CommandType => CommandType.GetBooksResponse;

    public override async Task ExecuteAsync(int correlationId, byte[] payload, CancellationToken ct = default)
    {
        _logger.LogInformation("Parsing command payload for GetBooksCommand.");

        var request = MemoryPack.MemoryPackSerializer.Deserialize<GetBooksRequestDto>(payload);
        byte[] responsePayload = [];

        if (request != null)
        {
            _logger.LogInformation("Requesting books for AssetId={AssetId} RequestCount={Count}.", request.AssetId, request.Count);
            var books = _objectStore.Get(request.AssetId, request.Count);
            _logger.LogInformation("({Count}) books found.", books.Length);
            var result = books.ToDto().ToArray();
            responsePayload = MemoryPack.MemoryPackSerializer.Serialize(result);
        }
        else
        {
            _logger.LogWarning("Cannot Deserialize GetBooksRequestDto. CorrelationId={CorrelationId}", correlationId);
        }

        _logger.LogInformation("Sending response payload for GetBooksCommand. Length={Length}.", responsePayload.Length);
        int totalLength = TcpConsts.MessageHeaderSize + responsePayload.Length;
        await ResponseWriter.WriteAsync(CommandType, totalLength, correlationId, responsePayload, ct);
    }
}
