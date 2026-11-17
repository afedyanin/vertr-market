using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

public class DummyRandomAlgo : PairTradingAlgoBase
{
    private const double ZscoreDelta = 5.0;

    private readonly double _entryThreshold;
    private readonly double _exitThreshold;
    private readonly decimal _requiredYQty;
    private readonly decimal _beta;

    public DummyRandomAlgo(AlgoMetadata metadata) : base(metadata)
    {
        _entryThreshold = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.EntryThreshold, 2.0);
        _exitThreshold = AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.ExitThreshold, 0.2);
        _requiredYQty = (decimal)AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.RequiredYQty, 1.0);
        _beta = (decimal)AlgoParams.GetValueOrDefault(AlgoMetadata.Keys.InitialBeta, 1.0);
    }

    protected override PairTradingSignal? ProcessTickX()
        => ProcessTick();

    protected override PairTradingSignal? ProcessTickY()
        => ProcessTick();

    private PairTradingSignal? ProcessTick()
    {
        if (_lastDepthX is null || _lastDepthY is null)
        {
            return null;
        }

        (var xQty, var yQty) = GetQty();

        if (xQty == 0 && yQty == 0)
        {
            return null;
        }

        var signal = new PairTradingSignal
        {
            StrategyId = StrategyId,
            BookX = _lastDepthX!,
            BookY = _lastDepthY!,
            XQtyLots = xQty,
            YQtyLots = yQty,
        };

        return signal;
    }
    private (int xQty, int yQty) GetQty()
    {
        var zScore = Random.Shared.NextDouble() * ZscoreDelta * 2 - ZscoreDelta;
        (var xQty, var yQty) = ApproximateLotCalculator.CalculateLots(0.85, 1, 1);

        if (zScore > _entryThreshold && !HasOpenPosition)
        {
            yQty *= -1;
        }
        else if (zScore < -_entryThreshold && !HasOpenPosition)
        {
            xQty *= -1;
        }
        else if (Math.Abs(zScore) < _exitThreshold && HasOpenPosition)
        {
            // Спред вернулся в норму, фиксируем прибыль
            yQty = -_openYQtyLots;
            xQty = -_openXQtyLots;
        }

        return (xQty, yQty);
    }
}
