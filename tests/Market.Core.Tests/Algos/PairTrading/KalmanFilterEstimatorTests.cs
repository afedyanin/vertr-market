namespace Market.Core.Tests.Algos.PairTrading;

public class KalmanFilterEstimatorTests
{
    [Test]
    public void KalmanFilterEstimatorSample()
    {
        // Симулируем цены двух коинтегрированных активов (например, BTC и ETH)
        // АssetY в среднем в 1.5 раза дороже AssetX + шум
        double[] assetX = { 100, 101, 100, 102, 104, 103, 105, 106, 104, 105 };
        double[] assetY = { 150, 152, 149, 153, 155, 156, 158, 159, 155, 157 };

        // Инициализация фильтра Калмана для оценки беты
        // Начальное предположение: Beta = 1.0, Ковариация ошибки = 1.0
        var kalman = new KalmanFilterEstimator(initialState: 1.0, initialErrorCovariance: 1.0);

        Console.WriteLine("Шаг\tЦена X\tЦена Y\t Beta\t Spread");
        Console.WriteLine("-----------------------------------");

        for (int i = 0; i < assetX.Length; i++)
        {
            double x = assetX[i];
            double y = assetY[i];

            // Обновляем фильтр текущими ценами и получаем новую бету
            double currentBeta = kalman.Update(x, y);

            // Расчет текущего спреда: Y - Beta * X
            double spread = y - (currentBeta * x);

            Console.WriteLine($"{i + 1}\t{x:F1}\t{y:F1}\t{currentBeta:F4}\t{spread:F4}");
        }
    }

    [TestCase(270 * 100, 28189)]
    [TestCase(28189, 270 * 100)]
    [TestCase(26189, 29900)]
    public void KalmanFilterEstimatorStep(double x, double y)
    {
        var kalman = new KalmanFilterEstimator(initialState: 1.0, initialErrorCovariance: 1.0);

        Console.WriteLine("Цена X\tЦена Y\t Beta\t Spread");
        Console.WriteLine("-----------------------------------");

        // Обновляем фильтр текущими ценами и получаем новую бету
        double currentBeta = kalman.Update(x, y);

        // Расчет текущего спреда: Y - Beta * X
        double spread = y - (currentBeta * x);

        Console.WriteLine($"{x:F1}\t{y:F1}\t{currentBeta:F4}\t{spread:F8}");
    }
}

/// <summary>
/// Облегченный фильтр Калмана для отслеживания динамической беты (отношения Y к X)
/// </summary>
public class KalmanFilterEstimator
{
    private double _beta; // Текущая оценка состояния (наша Beta)
    private double _p;    // Ковариация ошибки оценки
    private readonly double _q; // Волатильность процесса (насколько быстро бета может меняться)
    private readonly double _r; // Волатильность измерения (рыночный шум в спреде)

    public KalmanFilterEstimator(
        double initialState = 1.0,
        double initialErrorCovariance = 1.0)
    {
        _beta = initialState;
        _p = initialErrorCovariance;

        // Настройки чувствительности (гиперпараметры)
        _q = 0.0001; // Маленький Q означает, что истинное соотношение активов меняется медленно
        _r = 1.0;    // R определяет уровень случайного шума в ценах
    }

    public double Update(double priceX, double priceY)
    {
        // 1. Прогноз (Prediction)
        // В парном трейдинге мы предполагаем, что на следующем шаге бета останется прежней
        // Но ковариация ошибки увеличивается из-за неопределенности процесса (Q)
        _p += _q;

        // 2. Измерение (Measurement)
        // В качестве матрицы измерения H выступает цена актива X, так как Y = Beta * X
        double h = priceX;

        // Вычисляем ковариацию инновации (S = H*P*H^T + R)
        double s = h * _p * h + _r;

        // 3. Вычисление коэффициента усиления Калмана (Kalman Gain)
        // K = P * H^T * S^-1
        double k = (_p * h) / s;

        // Расчет ошибки прогноза (расхождение между реальным Y и прогнозным)
        double actualY = priceY;
        double predictedY = _beta * priceX;
        double innovation = actualY - predictedY;

        // 4. Коррекция (Update)
        // Обновляем бету на основе ошибки с весом коэффициента Калмана
        _beta += k * innovation;

        // Обновляем ковариацию ошибки для следующего шага
        _p = (1.0 - k * h) * _p;

        return _beta;
    }
}
