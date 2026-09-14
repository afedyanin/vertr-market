using System.Buffers;
using Market.ApiClient.Dtos;
using Market.ApiClient.Tcp;
using Market.Core.Abstractions;
using Market.Core.Converters;
using Market.Core.Models;

namespace Market.Core.Tcp.Commands;

internal sealed class PostTradesCommand : TradeCommandBase
{
    public override CommandType CommandType => CommandType.PostTrades;

    public PostTradesCommand(IObjectStore<TradeTick> tradesStore) : base(tradesStore)
    {
    }

    public override async Task ExecuteAsync(
        TcpResponseWriter responseWriter,
        int correlationId,
        ReadOnlySequence<byte> payload,
        CancellationToken ct = default)
    {
        var dtos = TcpPayload.Deserialize<TradeTickDto[]>(payload);
        var trades = dtos?.FromDto().ToArray();
        TradesStore.Set(trades ?? []);

        await WriteEmptyResponse(responseWriter, correlationId, ct);
    }
}
