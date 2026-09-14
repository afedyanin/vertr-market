using System.Buffers;
using Market.ApiClient.Tcp;
using Market.Core.Abstractions;
using Market.Core.Models;

namespace Market.Core.Tcp.Commands;

internal sealed class ClearTradesCommand : TradeCommandBase
{
    public ClearTradesCommand(IObjectStore<TradeTick> tradesStore) : base(tradesStore)
    {
    }

    public override CommandType CommandType => CommandType.ClearTrades;

    public override async Task ExecuteAsync(
        TcpResponseWriter responseWriter,
        int correlationId,
        ReadOnlySequence<byte> payload,
        CancellationToken ct = default)
    {
        TradesStore.Clear();
        await WriteEmptyResponse(responseWriter, correlationId, ct);
    }
}
