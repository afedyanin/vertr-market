using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.Abstractions;

public interface ICandlePublisher
{
    void Publish(in Candle candle);
}
