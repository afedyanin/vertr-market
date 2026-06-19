using System.Runtime.CompilerServices;
using Disruptor;
using Microsoft.Extensions.Logging;
using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.EventHandlers.Aggregated;

public sealed class CandleSpreadCalculator : IEventHandler<CandleEvent>
{
    private readonly ISpreadPublisher _publisher;
    private readonly ILogger<CandleSpreadCalculator> _logger;

    private readonly int _inboundAssetId1;
    private readonly int _inboundAssetId2;
    private const int OutboundAssetId = 1000;

    private Candle _inboundCandle1;
    private Candle _inboundCandle2;
    private decimal _lastPublishedSpread;
    private long _lastPublishedTicks;

    public CandleSpreadCalculator(
        ISpreadPublisher publisher,
        ILogger<CandleSpreadCalculator> logger,
        int inboundAssetId1,
        int inboundAssetId2)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _inboundAssetId1 = inboundAssetId1;
        _inboundAssetId2 = inboundAssetId2;

        _inboundCandle1 = new Candle { AssetId = inboundAssetId1 };
        _inboundCandle2 = new Candle { AssetId = inboundAssetId2 };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnEvent(CandleEvent data, long sequence, bool endOfBatch)
    {
        ProcessCandle(in data.Candle);
    }

    private void ProcessCandle(in Candle candle)
    {
        if (candle.AssetId != _inboundAssetId1 && candle.AssetId != _inboundAssetId2)
        {
            return;
        }

        if (candle.AssetId == _inboundAssetId1)
        {
            _inboundCandle1 = candle;
        }
        else
        {
            _inboundCandle2 = candle;
        }

        if (!_inboundCandle1.IsInitialized ||
            !_inboundCandle2.IsInitialized ||
            _inboundCandle1.OpenTime.Ticks != _inboundCandle2.OpenTime.Ticks)
        {
            return;
        }

        var spread = _inboundCandle2.Close - _inboundCandle1.Close;
        var currentTicks = _inboundCandle1.OpenTime.Ticks;

        if (currentTicks != _lastPublishedTicks || spread != _lastPublishedSpread)
        {
            _lastPublishedSpread = spread;
            _lastPublishedTicks = currentTicks;

            _publisher.Publish(OutboundAssetId, spread);
        }
    }
}

public interface ISpreadPublisher
{
    public void Publish(int assetId, decimal value);
}