using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.Abstractions;

public interface IOrderBookSnapshotPublisher
{
    void Publish(in OrderBook orderBook);
}
