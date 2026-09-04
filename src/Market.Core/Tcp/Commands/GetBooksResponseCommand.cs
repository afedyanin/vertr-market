using Market.ApiClient.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace Market.Core.Tcp.Commands;

internal sealed class GetBooksResponseCommand : CommandResponseBase
{
    public GetBooksResponseCommand(
        IServiceScope serviceScope,
        TcpResponseWriter responseWriter) : base(serviceScope, responseWriter)
    {
    }

    public override CommandType CommandType => CommandType.GetBooksResponse;

    public override async Task ExecuteAsync(int correlationId, byte[] payload, CancellationToken ct = default)
    {
        //var request = MemoryPack.MemoryPackSerializer.Deserialize<GetBooksRequestDto>(payload);

        // 2. Имитация долгого запроса (например, тяжелый I/O к БД на 500мс)
        // В этот момент поток чтения сокета РАБОТАЕТ и принимает другие команды!
        await Task.Delay(500, ct);

        //var mockResult = new MarketDepthDto[] { new MarketDepthDto { AssetId = request.AssetId, Price = 100.5m, Volume = 10 } };
        var mockResult = Array.Empty<MarketDepthDto>();

        // 3. Сериализация ответа
        byte[] responsePayload = MemoryPack.MemoryPackSerializer.Serialize(mockResult);
        int totalLength = TcpConsts.MessageHeaderSize + responsePayload.Length;

        await ResponseWriter.WriteAsync(CommandType, totalLength, correlationId, responsePayload, ct);
    }
}
