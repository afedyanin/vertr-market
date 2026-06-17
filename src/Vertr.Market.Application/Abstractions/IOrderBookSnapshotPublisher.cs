using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.Abstractions;

public interface IOrderBookSnapshotPublisher
{
    public void Publish(IEnumerable<OrderBook> books);
}
