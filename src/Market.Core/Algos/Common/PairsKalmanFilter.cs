namespace Market.Core.Algos.Common;

public class PairsKalmanFilter
{
    public readonly struct StepResult
    {
        public double Beta { get; }
        public double Alpha { get; }
        public double Spread { get; }
        public double ZScore { get; }

        public StepResult(double beta, double alpha, double spread, double zScore)
        {
            Beta = beta;
            Alpha = alpha;
            Spread = spread;
            ZScore = zScore;
        }
    }

    // Вектор состояний w = [beta, alpha]
    private double _beta;
    private double _alpha;

    // Элементы симметричной ковариационной матрицы ошибки P (2x2)
    private double _p00, _p01, _p10, _p11;

    // Диагональный элемент матрицы шума процесса Q
    private readonly double _q;

    // Дисперсия шума измерений R
    private readonly double _r;

    /// <summary>
    /// Инициализация фильтра Калмана для парного трейдинга.
    /// </summary>
    /// <param name="delta">Скорость адаптации параметров (обычно 1e-5 .. 1e-3).</param>
    /// <param name="obsNoise">Дисперсия шума наблюдений R (обычно 1.0).</param>
    public PairsKalmanFilter(double delta = 1e-4, double obsNoise = 1.0)
    {
        if (!double.IsFinite(delta) || delta <= 0.0 || delta >= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), delta, "Delta must be in the (0, 1) interval.");
        }

        if (!double.IsFinite(obsNoise) || obsNoise <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(obsNoise), obsNoise, "Observation noise must be positive and finite.");
        }

        _r = obsNoise;
        _beta = 0.0;
        _alpha = 0.0;

        // Начальная матрица ковариации P_0 = I * 1000
        _p00 = 1000.0;
        _p01 = 0.0;
        _p10 = 0.0;
        _p11 = 1000.0;

        _q = delta / (1.0 - delta);
    }

    /// <summary>
    /// Сбрасывает состояние фильтра в исходное. Полезно при обнаружении
    /// численной неустойчивости или после разрушения коинтеграции.
    /// </summary>
    public void Reset()
    {
        _beta = 0.0;
        _alpha = 0.0;
        _p00 = 1000.0;
        _p01 = 0.0;
        _p10 = 0.0;
        _p11 = 1000.0;
    }

    /// <summary>
    /// Обновление фильтра при поступлении новой пары цен (y, x).
    /// </summary>
    public StepResult Update(double y, double x)
    {
        if (!double.IsFinite(y))
        {
            throw new ArgumentException("Price y must be a finite number.", nameof(y));
        }

        if (!double.IsFinite(x))
        {
            throw new ArgumentException("Price x must be a finite number.", nameof(x));
        }

        // 1. Шаг прогнозирования (Predict): P_pred = P + Q
        double pp00 = _p00 + _q;
        double pp01 = _p01;
        double pp10 = _p10;
        double pp11 = _p11 + _q;

        // 2. Ошибка прогноза (спред e_t) при H = [x, 1.0]
        double yPred = x * _beta + _alpha;
        double spread = y - yPred;

        // 3. Дисперсия ошибки прогноза: S = H * P_pred * H^T + R
        double s = x * (x * pp00 + pp10) + (x * pp01 + pp11) + _r;

        // 4. Коэффициент усиления Калмана: K = (P_pred * H^T) / S
        // При вырожденной/нечисловой S обновление пропускается, чтобы не
        // распространять NaN/Infinity по состоянию фильтра.
        if (!double.IsFinite(s) || s <= 0.0)
        {
            return new StepResult(_beta, _alpha, spread, 0.0);
        }

        double k0 = (pp00 * x + pp01) / s;
        double k1 = (pp10 * x + pp11) / s;

        // 5. Шаг коррекции состояния: w = w_pred + K * spread
        _beta += k0 * spread;
        _alpha += k1 * spread;

        // 6. Шаг коррекции ковариации по формуле Джозефа:
        // P = (I - K*H) * P_pred * (I - K*H)^T + K * R * K^T
        double m00 = 1.0 - k0 * x;
        double m01 = -k0;
        double m10 = -k1 * x;
        double m11 = 1.0 - k1;

        // Промежуточное произведение T = M * P_pred
        double t00 = m00 * pp00 + m01 * pp10;
        double t01 = m00 * pp01 + m01 * pp11;
        double t10 = m10 * pp00 + m11 * pp10;
        double t11 = m10 * pp01 + m11 * pp11;

        // Финальный расчет P = T * M^T + K * R * K^T
        _p00 = t00 * m00 + t01 * m01 + k0 * _r * k0;
        _p01 = t00 * m10 + t01 * m11 + k0 * _r * k1;
        _p10 = t10 * m00 + t11 * m01 + k1 * _r * k0;
        _p11 = t10 * m10 + t11 * m11 + k1 * _r * k1;

        // 7. Расчет Z-score
        double zScore = spread / Math.Sqrt(s);

        return new StepResult(_beta, _alpha, spread, zScore);
    }
}
