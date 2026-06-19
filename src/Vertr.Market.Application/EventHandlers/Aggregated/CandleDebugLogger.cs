using Disruptor;
using Microsoft.Extensions.Logging;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.EventHandlers.Aggregated;

internal class CandleDebugLogger : IEventHandler<CandleEvent>
{
    private readonly ILogger<CandleDebugLogger> _logger;

    public CandleDebugLogger(Logger<CandleDebugLogger> logger)
    {
        _logger = logger;
    }

    public void OnEvent(CandleEvent data, long sequence, bool endOfBatch)
    {
        var candle = data.Candle;
        _logger.LogDebug($"Id={candle.AssetId} OpenTime={candle.OpenTime:O} O={candle.Open:F4} H={candle.High:F4} L={candle.Low:F4} C={candle.Close:F4} V={candle.Value} VL={candle.Value:F4}");
    }
}
