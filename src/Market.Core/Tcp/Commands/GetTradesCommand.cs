using System.Buffers;
using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Dtos;
using Market.Core.Abstractions;
using Market.Core.Converters;
using Market.Core.Models;

namespace Market.Core.Tcp.Commands;

internal sealed class GetTradesCommand : TradeCommandBase
{
    public override CommandType CommandType => CommandType.GetTradesResponse;

    public GetTradesCommand(IObjectStore<TradeTick> tradesStore) : base(tradesStore)
    {
    }

    public override async Task ExecuteAsync(
        TcpResponseWriter responseWriter,
        int correlationId,
        ReadOnlySequence<byte> payload,
        CancellationToken ct = default)
    {
        var request = TcpPayload.Deserialize<GetTradesRequestDto>(payload);
        byte[] responsePayload = [];

        if (request != null)
        {
            var trades = TradesStore.Get(request.AssetId, request.Count);
            var result = trades.ToDto().ToArray();
            responsePayload = MemoryPack.MemoryPackSerializer.Serialize(result);
        }

        int totalLength = TcpConsts.MessageHeaderSize + responsePayload.Length;
        await responseWriter.WriteAsync(CommandType, totalLength, correlationId, responsePayload, ct);
    }
}
