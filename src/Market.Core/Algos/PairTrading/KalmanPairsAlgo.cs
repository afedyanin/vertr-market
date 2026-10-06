using Market.Core.Models;

namespace Market.Core.Algos.PairTrading;

/// <summary>
/// Движок парного трейдинга на базе Фильтра Калмана с расчетом Z-Score
/// </summary>
public class KalmanPairsAlgo : PairTradingAlgoBase
{
    private double _beta;      // Текущая Beta (соотношение активов)
    private double _p;         // Ковариация ошибки оценки беты
    private readonly double _q;// Волатильность изменения беты (системный шум)
    private readonly double _r;// Волатильность цен (рыночный шум)

    // Пороговые значения для генерации сигналов
    private const double EntryThreshold = 2.0;  // Вход при Z-Score > 2.0 или < -2.0
    private const double ExitThreshold = 0.5;   // Выход (тейк-профит) при приближении к 0

    public KalmanPairsAlgo(
        ushort assetX,
        ushort assetY,
        double initialBeta = 1.0,
        double q = 0.0001,
        double r = 1.0) : base(assetX, assetY)
    {
        _beta = initialBeta;
        _p = 1.0;
        _q = q;
        _r = r;
    }

    protected override ITradingSignal? ProcessTick(DateTime timestamp)
    {
        // Пока не получили данные по обоим инструментам — расчет невозможен
        if (_lastDepthX == null || _lastDepthY == null)
        {
            return null;
        }

        // --- 1. КЛАССИЧЕСКИЙ ШАГ КАЛМАНА ---
        _p += _q; // Прогноз ковариации

        var priceX = _lastDepthX.MidPrice!.Value;
        var priceY = _lastDepthY.MidPrice!.Value;

        double h = priceX;
        double s = h * _p * h + _r; // Ковариация инновации (по сути, дисперсия текущего спреда)
        double k = (_p * h) / s;    // Коэффициент Калмана

        double innovation = priceY - (_beta * priceX); // Текущая ошибка (сырой спред)

        _beta += k * innovation; // Коррекция беты
        _p = (1.0 - k * h) * _p;        // Коррекция ковариации ошибки

        // --- 2. РАСЧЕТ Z-SCORE ---
        // Корень из S (ковариации инновации) дает нам текущее стандартное отклонение спреда (сигму)
        // с учетом рыночного шума R. Это избавляет от необходимости считать скользящее окно.
        double sigma = Math.Sqrt(s);
        double zScore = innovation / sigma;

        // --- 3. ГЕНЕРАЦИЯ СИГНАЛОВ ---
        string signal = "HOLD";

        if (zScore > EntryThreshold)
        {
            signal = "SHORT Y / LONG X"; // Спред слишком большой, ставим на сужение
            _currentPosition = PositionState.ShortY_LongX;
        }
        else if (zScore < -EntryThreshold)
        {
            signal = "LONG Y / SHORT X"; // Спред слишком маленький, ставим на расширение
            _currentPosition = PositionState.LongY_ShortX;
        }
        else if (Math.Abs(zScore) < ExitThreshold && _currentPosition != PositionState.None)
        {
            signal = "EXIT POSITIONS";   // Спред вернулся в норму, фиксируем прибыль
            _currentPosition = PositionState.None;
        }

        return new KalmanPairsAlgoSignal
        {
            Beta = _beta,
            Spread = innovation,
            ZScore = zScore,
            Signal = signal,
            AssetX = AssetX,
            AssetY = AssetY,
            PriceX = priceX,
            PriceY = priceY,
        };
    }
}

public record KalmanPairsAlgoSignal : ITradingSignal
{
    public double Beta { get; set; }
    public double Spread { get; set; }
    public double ZScore { get; set; }
    public string Signal { get; set; } = "HOLD";
    public ushort AssetX { get; set; }
    public ushort AssetY { get; set; }
    public double PriceX { get; set; }
    public double PriceY { get; set; }
}
