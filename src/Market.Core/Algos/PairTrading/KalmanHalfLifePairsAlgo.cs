using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

public class KalmanHalfLifePairsAlgo : PairTradingAlgoBase
{
    // Калман 1: Оценка беты активов
    private double _beta;
    private double _pBeta = 1.0;
    private const double QBeta = 0.00005; // На секундах бета почти статична
    private const double RBeta = 0.01;

    // Калман 2: Онлайн-оценка AR(1) для спреда (Модель Орнштейна-Уленбека)
    private double _prevSpread = 0.0;
    private double _phi = 0.9; // Авторегрессионный коэффициент (должен быть < 1.0 для коинтеграции)
    private double _pPhi = 0.1;
    private const double QPhi = 0.001;  // Чувствительность к изменению скорости возврата
    private const double RPhi = 0.05;

    private PositionState _currentPosition = PositionState.None;

    private int _secondsInTrade = 0;


    public KalmanHalfLifePairsAlgo(
        ushort assetX,
        ushort assetY,
        double initialBeta = 1.0) : base(assetX, assetY)
    {
        _beta = initialBeta;
    }

    protected override ITradingSignal? ProcessTick(DateTime timestamp)
    {
        // Пока не получили данные по обоим инструментам — расчет невозможен
        if (_lastDepthX == null || _lastDepthY == null)
        {
            return null;
        }

        var priceX = _lastDepthX.MidPrice!.Value;
        var priceY = _lastDepthY.MidPrice!.Value;

        // 1. Обновляем Бету и вычисляем текущий Спред
        _pBeta += QBeta;
        double sBeta = priceX * _pBeta * priceX + RBeta;
        double kBeta = (_pBeta * priceX) / sBeta;
        double spread = priceY - (_beta * priceX);
        _beta += kBeta * spread;
        _pBeta = (1.0 - kBeta * priceX) * _pBeta;

        // 2. Оценка коинтеграции и скорости возврата (OU-процесс) через AR(1) спреда: Спред_t = Phi * Спред_{t-1}
        double kappa = 0.0;
        double halfLife = double.PositiveInfinity;
        bool isCointegrated = false;

        if (_prevSpread != 0.0)
        {
            _pPhi += QPhi;
            double sPhi = _prevSpread * _pPhi * _prevSpread + RPhi;
            double kPhi = (_pPhi * _prevSpread) / sPhi;

            // Корректируем Phi на основе того, насколько спред последовал за предыдущим значением
            _phi += kPhi * (spread - (_phi * _prevSpread));
            _pPhi = (1.0 - kPhi * _prevSpread) * _pPhi;

            // Ограничиваем Phi физическими границами во избежание взрыва алгоритма
            _phi = Math.Clamp(_phi, -0.999, 1.5);

            // Переводим параметр AR(1) в непрерывную скорость возврата OU-процесса (Kappa)
            // Если Phi >= 1, ряд нестационарен (коинтеграция разрушена)
            if (_phi > 0 && _phi < 1.0)
            {
                kappa = -Math.Log(_phi); // Дискретный шаг = 1 секунда, delta_t не нужна
                halfLife = Math.Log(2.0) / kappa;
                isCointegrated = true;
            }
        }

        _prevSpread = spread;

        // 3. РАСЧЕТ РЕАЛЬНЫХ ТОРГОВЫХ СПРЕДОВ С УЧЕТОМ BID/ASK
        // Спред, если мы заходим в LONG Y и SHORT X (Покупаем Y по Ask, продаем X по Bid)
        var spreadBuyY_SellX = _lastDepthY.BestAsk - (_beta * _lastDepthX.BestBid);

        // Спред, если мы заходим в SHORT Y и LONG X (Продаем Y по Bid, покупаем X по Ask)
        var spreadSellY_BuyX = _lastDepthY.BestBid - (_beta * _lastDepthX.BestAsk);

        // Переводим в Z-Score, используя общую сигму фильтра Калмана
        var sigma = Math.Sqrt(sBeta);
        var zScoreBuy = spreadBuyY_SellX / sigma;
        var zScoreSell = spreadSellY_BuyX / sigma;

        // 3. Логика сигналов с контролем полураспада
        string signal = "HOLD";

        if (_currentPosition == PositionState.None && isCointegrated && halfLife > 1.0 && halfLife < 60.0)
        {
            // Вход в Short Y / Long X имеет смысл, только если спред ПРОДАЖИ (Sell) превысил верхний порог.
            // Мы будем бить по стакану: продавать Y по Bid и покупать X по Ask.
            if (zScoreSell > 2.0)
            {
                signal = $"EXECUTE: SHORT Y @ {_lastDepthY.BestBid} / LONG X @ {_lastDepthX.BestAsk}";
                _currentPosition = PositionState.ShortY_LongX;
                _secondsInTrade = 0;
            }
            // Вход в Long Y / Short X: покупаем Y по Ask, продаем X по Bid.
            else if (zScoreBuy < -2.0)
            {
                signal = $"EXECUTE: LONG Y @ {_lastDepthY.BestAsk} / SHORT X @ {_lastDepthX.BestBid}";
                _currentPosition = PositionState.LongY_ShortX;
                _secondsInTrade = 0;
            }
        }
        else if (_currentPosition == PositionState.ShortY_LongX)
        {
            _secondsInTrade++;

            // СИГНАЛ ВЫХОДА ПО ПОЛУРАСПАДУ (Time Stop): 
            // Если позиция висит дольше, чем 2-3 периода полураспада, математическое ожидание прибыли исчезает.
            // Либо если коинтеграция полностью исчезла (isCointegrated == false)
            // Выход из позиции Short Y / Long X означает, что нам нужно КУПИТЬ Y (по Ask) и ПРОДАТЬ X (по Bid).
            // Поэтому мы отслеживаем zScoreBuy для выхода.
            if (zScoreBuy <= 0.2 || !isCointegrated || _secondsInTrade > (halfLife * 2.5))
            {
                signal = $"EXIT: BUY Y @ {_lastDepthY.BestAsk} / SELL X @ {_lastDepthX.BestBid} (TAKE PROFIT)";
                _currentPosition = PositionState.None;
                _secondsInTrade = 0;
            }
        }
        else if (_currentPosition == PositionState.LongY_ShortX)
        {
            _secondsInTrade++;

            // Выход из позиции Long Y / Short X: ПРОДАЕМ Y (по Bid) и ПОКУПАЕМ X (по Ask).
            if (zScoreSell >= -0.2 || !isCointegrated || _secondsInTrade > (halfLife * 2.5))
            {
                signal = $"EXIT: SELL Y @ {_lastDepthY.BestBid} / BUY X @ {_lastDepthX.BestAsk} (TAKE PROFIT)";
                _currentPosition = PositionState.None;
                _secondsInTrade = 0;
            }
        }

        return new KalmanHalfLifePairsSignal
        {
            Beta = _beta,
            Spread = spread,
            ZScoreBuy = zScoreBuy,
            ZScoreSell = zScoreSell,
            Signal = signal,
            AssetX = AssetX,
            AssetY = AssetY,
            Kappa = kappa,
            HalfLifeSeconds = halfLife,
            IsCointegrated = isCointegrated,
        };
    }
}

public record KalmanHalfLifePairsSignal : ITradingSignal
{
    public double Beta { get; set; }
    public double Spread { get; set; }
    public double? ZScoreBuy { get; set; }
    public double? ZScoreSell { get; set; }
    public string Signal { get; set; } = "HOLD";
    public ushort AssetX { get; set; }
    public ushort AssetY { get; set; }
    public double Kappa { get; set; }
    public double HalfLifeSeconds { get; set; }
    public bool IsCointegrated { get; set; }
}
