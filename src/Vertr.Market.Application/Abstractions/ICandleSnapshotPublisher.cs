using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.Abstractions;

public interface ICandleSnapshotPublisher
{
    Task PublishAsync(ReadOnlyMemory<Candle> candles, CancellationToken ct);
}
