using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.Abstractions;

public interface IOrderBookSnapshotPublisher
{
    Task PublishAsync(ReadOnlyMemory<OrderBook> books, CancellationToken ct);
}
