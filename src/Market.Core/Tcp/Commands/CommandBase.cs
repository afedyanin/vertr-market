using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace Market.Core.Tcp.Commands;

internal abstract class CommandBase
{
    protected TcpResponseWriter ResponseWriter { get; private set; }

    protected IServiceScope ServiceScope { get; private set; }

    public abstract CommandType CommandType { get; }

    protected CommandBase(
        IServiceScope serviceScope,
        TcpResponseWriter responseWriter)
    {
        ResponseWriter = responseWriter;
        ServiceScope = serviceScope;
    }

    public abstract Task ExecuteAsync(
        int correlationId,
        byte[] payload,
        CancellationToken ct = default);

    protected virtual async Task WriteEmptyResponse(int correlationId, CancellationToken ct = default)
    {
        var responsePayload = MemoryPack.MemoryPackSerializer.Serialize(new EmptyDto());
        int totalLength = TcpConsts.MessageHeaderSize + responsePayload.Length;
        await ResponseWriter.WriteAsync(CommandType, totalLength, correlationId, responsePayload, ct);
    }
}
