namespace Market.Core.Algos.Common;

/// <summary>
/// Облегченный фильтр Калмана для отслеживания динамической беты (отношения Y к X)
/// </summary>
public class KalmanFilterEstimator
{
    private readonly double _q; // Волатильность процесса (насколько быстро бета может меняться)
    private readonly double _r; // Волатильность измерения (рыночный шум в спреде)

    public double Beta { get; private set; }
    public double P { get; private set; }
    public double H { get; private set; }
    public double S { get; private set; }
    public double K { get; private set; }
    public double Spread { get; private set; }
    public double ZScore { get; private set; }

    public KalmanFilterEstimator(
        double initialBeta = 1.0,
        double initialErrorCovariance = 1.0,
        double q = 0.0001,
        double r = 1.0)
    {
        // Настройки чувствительности (гиперпараметры)
        _q = q; // Маленький Q означает, что истинное соотношение активов меняется медленно
        _r = r; // R определяет уровень случайного шума в ценах

        Beta = initialBeta;
        P = initialErrorCovariance;
    }

    public void Update(double priceX, double priceY)
    {
        // Ковариация ошибки оценки
        P += _q;

        // Измерение (Measurement)
        H = priceX;

        // Ковариация инновации (S = H*P*H^T + R)
        S = H * P * H + _r;

        // Коэффициент усиления Калмана (Kalman Gain)
        K = (P * H) / S;

        // Расчет ошибки прогноза (расхождение между реальным Y и прогнозным)
        Spread = priceY - Beta * priceX;

        // Обновляем бету на основе ошибки с весом коэффициента Калмана
        Beta += K * Spread;

        // Обновляем ковариацию ошибки для следующего шага
        P = (1.0 - K * H) * P;

        // --- РАСЧЕТ Z-SCORE ---
        // Корень из S (ковариации инновации) дает нам текущее стандартное отклонение спреда (сигму)
        // с учетом рыночного шума R. Это избавляет от необходимости считать скользящее окно.
        ZScore = Spread / Math.Sqrt(S);
    }
}
