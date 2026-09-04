using Microsoft.Extensions.DependencyInjection;

namespace Market.Core.Tcp.Commands;

internal static class CommandResponseFactory
{
    public static CommandResponseBase? CreateCommand(
        CommandType requestCommand,
        IServiceScope serviceScope,
        TcpResponseWriter responseWriter)
    {
        return requestCommand switch
        {
            CommandType.GetBooksRequest
                => new GetBooksResponseCommand(serviceScope, responseWriter),
            CommandType.PostBooks
                => new PostBooksResponseCommand(serviceScope, responseWriter),
            _
                => default,
        };
    }
}
