namespace Market.Core.PairTrading;

/// <summary>
/// Движок парного трейдинга на базе Фильтра Калмана с расчетом Z-Score
/// </summary>
public class KalmanPairsEngine
{
    private double _beta;      // Текущая Beta (соотношение активов)
    private double _p;         // Ковариация ошибки оценки беты
    private readonly double _q;// Волатильность изменения беты (системный шум)
    private readonly double _r;// Волатильность цен (рыночный шум)

    // Пороговые значения для генерации сигналов
    private const double EntryThreshold = 2.0;  // Вход при Z-Score > 2.0 или < -2.0
    private const double ExitThreshold = 0.5;   // Выход (тейк-профит) при приближении к 0

    public KalmanPairsEngine(double initialBeta = 1.0, double q = 0.0001, double r = 1.0)
    {
        _beta = initialBeta;
        _p = 1.0;
        _q = q;
        _r = r;
    }

    public TradingResult ProcessTick(double priceX, double priceY)
    {
        // --- 1. КЛАССИЧЕСКИЙ ШАГ КАЛМАНА ---
        _p += _q; // Прогноз ковариации

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
        }
        else if (zScore < -EntryThreshold)
        {
            signal = "LONG Y / SHORT X"; // Спред слишком маленький, ставим на расширение
        }
        else if (Math.Abs(zScore) < ExitThreshold)
        {
            signal = "EXIT POSITIONS";   // Спред вернулся в норму, фиксируем прибыль
        }

        return new TradingResult
        {
            Beta = _beta,
            Spread = innovation,
            ZScore = zScore,
            Signal = signal
        };
    }
}

public struct TradingResult
{
    public double Beta { get; set; }
    public double Spread { get; set; }
    public double ZScore { get; set; }
    public string Signal { get; set; }
}
