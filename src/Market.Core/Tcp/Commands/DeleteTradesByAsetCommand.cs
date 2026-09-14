using System.Buffers;
using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Dtos;
using Market.Core.Abstractions;
using Market.Core.Models;

namespace Market.Core.Tcp.Commands;

internal sealed class DeleteTradesByAsetCommand : TradeCommandBase
{
    public override CommandType CommandType => CommandType.DeleteTradesByAsset;

    public DeleteTradesByAsetCommand(IObjectStore<TradeTick> tradesStore) : base(tradesStore)
    {
    }

    public override async Task ExecuteAsync(
        TcpResponseWriter responseWriter,
        int correlationId,
        ReadOnlySequence<byte> payload,
        CancellationToken ct = default)
    {
        var request = TcpPayload.Deserialize<DeleteTradesRequestDto>(payload);

        if (request != null)
        {
            TradesStore.Delete(request.AssetId);
        }

        await WriteEmptyResponse(responseWriter, correlationId, ct);
    }
}
