using Market.ApiClient.Tcp;
using Microsoft.Extensions.DependencyInjection;

namespace Market.Core.Tcp.Commands;

internal static class CommandFactory
{
    public static CommandBase? CreateCommand(
        CommandType requestCommand,
        IServiceScope serviceScope,
        TcpResponseWriter responseWriter)
    {
        return requestCommand switch
        {
            CommandType.GetBooksRequest
                => new GetBooksCommand(serviceScope, responseWriter),
            CommandType.PostBooks
                => new PostBooksCommand(serviceScope, responseWriter),
            CommandType.DeleteBooksByAsset
                => new DeleteBooksByAssetCommand(serviceScope, responseWriter),
            CommandType.ClearBooks
                => new ClearBooksCommand(serviceScope, responseWriter),
            _
                => default,
        };
    }
}
