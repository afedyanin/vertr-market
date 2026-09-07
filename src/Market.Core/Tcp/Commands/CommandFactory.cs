using Market.ApiClient.Tcp;
using Market.Core.Abstractions;
using Market.Core.Models;

namespace Market.Core.Tcp.Commands;

public class CommandFactory
{
    private readonly Dictionary<CommandType, CommandBase> _commands;

    public CommandFactory(IObjectStore<MarketDepth> booksStore)
    {
        _commands = new Dictionary<CommandType, CommandBase>
        {
            [CommandType.GetBooksRequest] = new GetBooksCommand(booksStore),
            [CommandType.PostBooks] = new PostBooksCommand(booksStore),
            [CommandType.DeleteBooksByAsset] = new DeleteBooksByAssetCommand(booksStore),
            [CommandType.ClearBooks] = new ClearBooksCommand(booksStore)
        };
    }

    public CommandBase? CreateCommand(CommandType requestCommand)
    {
        _commands.TryGetValue(requestCommand, out var command);
        return command;
    }
}
