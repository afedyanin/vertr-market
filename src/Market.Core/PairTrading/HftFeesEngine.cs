namespace Market.Core.PairTrading;


/*
1. Масштабирование беты: Комиссия по активу X зависит от объема, который мы в нем удерживаем. 
Так как объем X равен Volume_Y / Beta, то при переносе затрат на общую шкалу (в единицы цены актива Y), 
комиссия X умножается на текущую Beta (2.0 * _beta * (Price_X * Fee_X)).

2. Фактор «2.0»: Код умножает базовую комиссию на 2, поскольку в рамках одной рыночной итерации (круг «круг-трейд») 
вы платите за открытие (Taker ордер) и гарантированно заложите наихудший сценарий закрытия по рынку (тоже Taker).

3. Фильтрация микро-арбитража: Если запустить этот тест, вы увидите, как RawZScoreSell может быть равен 2.2 
(что в обычной системе вызвало бы сделку), но ZScoreSellWithFees окажется на уровне 1.85. 
Робот останется в режиме HOLD. 
Это защитит торговый баланс от «перемалывания» комиссиями (Overtrading) на высокочастотных фреймах.
*/

public class HftFeesEngine
{
    private double _beta;
    private double _pBeta = 1.0;
    private const double QBeta = 0.00005;
    private const double RBeta = 0.01;

    private readonly double _tradeVolumeY;
    private readonly double _feeRateX; // Комиссия X (в долях: 0.0005 = 0.05%)
    private readonly double _feeRateY; // Комиссия Y (в долях)

    private PositionState _currentPosition = PositionState.None;

    public HftFeesEngine(double initialBeta, double tradeVolumeY, double feeRateX, double feeRateY)
    {
        _beta = initialBeta;
        _tradeVolumeY = tradeVolumeY;
        _feeRateX = feeRateX;
        _feeRateY = feeRateY;
    }

    public FeesResult ProcessSecond(OrderBookDepth x, OrderBookDepth y)
    {
        // 1. VWAP расчеты
        double vwapAskY = y.GetVwap(_tradeVolumeY, isBuy: true);
        double vwapBidY = y.GetVwap(_tradeVolumeY, isBuy: false);
        double midVwapY = (vwapAskY + vwapBidY) / 2.0;

        double requiredVolumeX = _tradeVolumeY / _beta;
        double vwapAskX = x.GetVwap(requiredVolumeX, isBuy: true);
        double vwapBidX = x.GetVwap(requiredVolumeX, isBuy: false);
        double midVwapX = (vwapAskX + vwapBidX) / 2.0;

        // 2. Обновление Калмана
        _pBeta += QBeta;
        double sBeta = midVwapX * _pBeta * midVwapX + RBeta;
        double kBeta = (_pBeta * midVwapX) / sBeta;
        double midSpread = midVwapY - (_beta * midVwapX);
        _beta += kBeta * midSpread;
        _pBeta = (1.0 - kBeta * midVwapX) * _pBeta;

        // 3. Базовый расчет спредов по ценам стакана (без учета комиссий)
        double spreadBuyY_SellX = vwapAskY - (_beta * vwapBidX);
        double spreadSellY_BuyX = vwapBidY - (_beta * vwapAskX);

        double sigma = Math.Sqrt(sBeta);
        double rawZScoreBuy = spreadBuyY_SellX / sigma;
        double rawZScoreSell = spreadSellY_BuyX / sigma;

        // 4. УЧЕТ ТРАНЗАКЦИОННЫХ ИЗДЕРЖЕК (FEES)
        // Считаем комиссии на "полный круг" (Вход + Выход) для одной единицы спреда.
        // Формула стоимости комиссий в пунктах цены актива Y:
        // Расходы_Y = 2 * (Price_Y * Fee_Y) + 2 * Beta * (Price_X * Fee_X)
        double totalFeesInY = 2.0 * (midVwapY * _feeRateY) + 2.0 * _beta * (midVwapX * _feeRateX);

        // Переводим стоимость комиссий из пунктов цены в единицы Z-Score
        double feesInZScore = totalFeesInY / sigma;

        // Скорректированные Z-Score. 
        // Комиссия всегда уменьшает нашу прибыль, сдвигая порог входа дальше.
        double zScoreBuyWithFees = rawZScoreBuy + feesInZScore;   // Сдвигаем вниз (в сторону еще больших отрицательных значений)
        double zScoreSellWithFees = rawZScoreSell - feesInZScore; // Сдвигаем вниз (уменьшаем профитный потенциал шорта)

        // 5. ТОРГОВАЯ ЛОГИКА
        string signal = "HOLD";

        if (_currentPosition == PositionState.None)
        {
            // Используем скорректированный Z-Score для принятия решения о входе
            if (zScoreSellWithFees > 2.0)
            {
                signal = $"EXECUTE SHORT Y / LONG X (Friction Passed)";
                _currentPosition = PositionState.ShortY_LongX;
            }
            else if (zScoreBuyWithFees < -2.0)
            {
                signal = $"EXECUTE LONG Y / SHORT X (Friction Passed)";
                _currentPosition = PositionState.LongY_ShortX;
            }
        }
        else if (_currentPosition == PositionState.ShortY_LongX && rawZScoreBuy <= 0.2)
        {
            // Выход контролируем по "чистому" спреду, так как комиссия на выход уже заложена в фильтр на этапе входа
            signal = $"EXIT POSITION";
            _currentPosition = PositionState.None;
        }
        else if (_currentPosition == PositionState.LongY_ShortX && rawZScoreSell >= -0.2)
        {
            signal = $"EXIT POSITION";
            _currentPosition = PositionState.None;
        }

        return new FeesResult
        {
            Beta = _beta,
            RawZScoreBuy = rawZScoreBuy,
            RawZScoreSell = rawZScoreSell,
            ZScoreBuyWithFees = zScoreBuyWithFees,
            ZScoreSellWithFees = zScoreSellWithFees,
            TotalFeesInY = totalFeesInY,
            Signal = signal
        };
    }
}

public struct FeesResult
{
    public double Beta { get; set; }
    public double RawZScoreBuy { get; set; }
    public double RawZScoreSell { get; set; }
    public double ZScoreBuyWithFees { get; set; }
    public double ZScoreSellWithFees { get; set; }
    public double TotalFeesInY { get; set; }
    public string Signal { get; set; }
}

