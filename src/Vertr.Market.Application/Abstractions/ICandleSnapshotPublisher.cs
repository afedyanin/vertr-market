using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.Abstractions;

public interface ICandleSnapshotPublisher
{
    void Publish(in Candle candle);
}
