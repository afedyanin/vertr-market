using Market.ApiClient.Tcp;
using Microsoft.Extensions.DependencyInjection;

namespace Market.Core.Tcp.Commands;

internal abstract class CommandResponseBase
{
    protected TcpResponseWriter ResponseWriter { get; private set; }

    protected IServiceScope ServiceScope { get; private set; }

    public abstract CommandType CommandType { get; }

    protected CommandResponseBase(
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
}
